using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReactL.api.Common.Exceptions;
using ReactL.api.Common.Settings;
using ReactL.api.Data;
using ReactL.api.DTOs.Ai;
using ReactL.api.Models.Conversations;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace ReactL.api.Services.Ai
{
    /// <summary>
    /// 多提供商 AI 服務實作，支援 SSE 串流（chat/completions 的 stream=true）
    /// 透過 IHttpClientFactory 管理 HttpClient 生命週期，避免 Socket 耗盡問題
    /// </summary>
    public class OpenAiService : IAiService
    {
        private readonly AppDbContext _db;
        private readonly AiSettings _aiSettings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<OpenAiService> _logger;

        public OpenAiService(
            AppDbContext db,
            IOptions<AiSettings> aiSettings,
            IHttpClientFactory httpClientFactory,
            ILogger<OpenAiService> logger)
        {
            _db = db;
            _aiSettings = aiSettings.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async IAsyncEnumerable<ChatStreamChunk> ChatStreamAsync(
            ChatRequest request,
            Guid userId,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // 取得對話與所有歷史訊息（含 SystemPrompt）
            var conversation = await _db.Conversations
                .AsNoTracking()
                .Include(c => c.Persona)
                .Include(c => c.Messages.OrderBy(m => m.CreatedAt))
                .Where(c => c.Id == request.ConversationId && c.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new NotFoundException("Conversation", request.ConversationId);

            var rawModel = request.ModelOverride ?? conversation.ModelType;
            var (providerId, modelName) = ParseModelType(rawModel);
            var providerConfig = _aiSettings.Providers.FirstOrDefault(p => p.Id == providerId);
            var providerDisplay = providerConfig?.DisplayName ?? providerId;
            // stream_options.include_usage 只有 Groq 等特定提供商支援，其他提供商傳此欄位會回傳 400
            var supportsStreamUsage = providerConfig?.SupportsStreamUsage ?? false;
            var messages = BuildMessages(conversation, request.UserMessage);

            // C-05：對話開始前無任何訊息且標題為預設值，串流結束後自動產生標題
            bool isFirstExchange = conversation.Messages.Count == 0 && conversation.Title == "新對話";

            // 先將 user 訊息寫入 DB（不等 AI 回應完才存）
            var userMsg = new Message
            {
                ConversationId = conversation.Id,
                Role = "user",
                Content = request.UserMessage
            };
            _db.Messages.Add(userMsg);
            await _db.SaveChangesAsync(cancellationToken);

            var fullContent = new StringBuilder();
            int tokensIn = 0, tokensOut = 0;
            // 先收集 done chunk，等 DB 寫入完成後再 yield
            // 確保前端 invalidateQueries 觸發時 DB 已是最新狀態（含標題）
            ChatStreamChunk? doneChunk = null;

            await foreach (var chunk in CallOpenAiStreamAsync(providerId, modelName, providerDisplay, messages, supportsStreamUsage, cancellationToken))
            {
                if (chunk.Type == "delta" && chunk.Content != null)
                {
                    fullContent.Append(chunk.Content);
                    yield return chunk;
                }
                else if (chunk.Type == "done" && chunk.Usage != null)
                {
                    tokensIn = chunk.Usage.TokensIn;
                    tokensOut = chunk.Usage.TokensOut;
                    // 不立即 yield，等後續 DB 操作與標題生成完成後統一送出
                    doneChunk = chunk;
                }
                else if (chunk.Type == "error" || chunk.Type == "rate_limit")
                {
                    yield return chunk;
                    yield break;
                }
            }

            // 串流結束後，將 AI 回應與 Token 用量寫入 DB
            if (fullContent.Length > 0)
            {
                userMsg.TokensIn = tokensIn;
                var assistantMsg = new Message
                {
                    ConversationId = conversation.Id,
                    Role = "assistant",
                    Content = fullContent.ToString(),
                    TokensOut = tokensOut
                };
                _db.Messages.Add(assistantMsg);
                await _db.SaveChangesAsync(CancellationToken.None);
            }

            // C-05：第一輪對話完成，呼叫 AI 產生標題並寫入 DB
            // title_updated 在 done 之前 yield，使前端 done 的 invalidateQueries 能取到最新標題
            if (doneChunk != null && isFirstExchange && fullContent.Length > 0)
            {
                try
                {
                    const string titleSystem =
                        "你是一個對話標題生成器。根據使用者的第一則訊息，生成一個簡潔的繁體中文標題（10~20 字）。" +
                        "精確描述對話主題，只輸出標題文字，不要加引號、序號或任何說明。";
                    var titleUser = $"使用者訊息：{request.UserMessage}";

                    var newTitle = (await _ai.CompleteAsync(titleSystem, titleUser))
                        .Trim().Trim('"').Trim('「').Trim('」');

                    if (!string.IsNullOrWhiteSpace(newTitle))
                    {
                        // 再次確認標題仍為預設，避免使用者在串流期間手動改名被覆蓋
                        var convToUpdate = await _db.Conversations.FindAsync(conversation.Id);
                        if (convToUpdate != null && convToUpdate.Title == "新對話")
                        {
                            convToUpdate.Title = newTitle;
                            await _db.SaveChangesAsync(CancellationToken.None);
                        }
                        yield return new ChatStreamChunk { Type = "title_updated", Content = newTitle };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "C-05 自動產生對話標題失敗，跳過");
                }
            }

            // 所有 DB 操作完成後才 yield done，確保前端 invalidateQueries 取到完整資料
            if (doneChunk != null)
                yield return doneChunk;
        }

        public async Task<string> CompleteAsync(
            string systemPrompt,
            string userPrompt,
            CancellationToken cancellationToken = default)
        {
            var (providerId, modelName) = ParseModelType(_aiSettings.DefaultModel);
            var messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            };

            var body = new
            {
                model = modelName,
                messages,
                max_tokens = _aiSettings.MaxTokens,
                stream = false
            };

            var client = _httpClientFactory.CreateClient($"ai:{providerId}");
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync("chat/completions", content, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;
        }

        // ── 私有輔助方法 ──────────────────────────────────────────────────────

        /// <summary>
        /// 解析 "providerId:modelId" 格式；舊格式或無冒號時 fallback 至 groq
        /// </summary>
        private static (string providerId, string modelName) ParseModelType(string modelType)
        {
            var idx = modelType.IndexOf(':');
            if (idx > 0 && idx < modelType.Length - 1)
                return (modelType[..idx], modelType[(idx + 1)..]);
            // Legacy format or fallback: use first configured provider
            return ("groq", modelType);
        }

        private static List<object> BuildMessages(
            Models.Conversations.Conversation conversation,
            string newUserMessage)
        {
            var messages = new List<object>();

            if (conversation.Persona?.SystemPrompt is { Length: > 0 } systemPrompt)
                messages.Add(new { role = "system", content = systemPrompt });

            foreach (var m in conversation.Messages)
                messages.Add(new { role = m.Role, content = m.Content });

            messages.Add(new { role = "user", content = newUserMessage });
            return messages;
        }

        private async IAsyncEnumerable<ChatStreamChunk> CallOpenAiStreamAsync(
            string providerId,
            string modelName,
            string providerDisplay,
            List<object> messages,
            bool supportsStreamUsage,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // stream_options 只傳給支援的提供商，否則 Cerebras / SambaNova 等會回傳 400
            object body = supportsStreamUsage
                ? (object)new { model = modelName, messages, max_tokens = _aiSettings.MaxTokens, stream = true, stream_options = new { include_usage = true } }
                : new { model = modelName, messages, max_tokens = _aiSettings.MaxTokens, stream = true };

            var client = _httpClientFactory.CreateClient($"ai:{providerId}");

            // Content-Type 必須是純 application/json，不能帶 charset=utf-8，否則部分 CDN 會誤判路由
            var requestContent = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8);
            requestContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = requestContent
            };

            // 確保每次請求都從 IOptions 取最新 Key，不依賴 HttpClient 啟動時的快照
            if (_aiSettings.ProviderKeys.TryGetValue(providerId, out var providerKey) && !string.IsNullOrEmpty(providerKey))
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", providerKey);

            // yield 不能在 catch 內使用，改用 bool 旗標與錯誤訊息傳遞到 yield 區段
            HttpResponseMessage? response = null;
            Exception? callError = null;
            string? friendlyError = null;
            bool isRateLimit = false;

            try
            {
                // ResponseHeadersRead：不等整個串流結束才回傳，收到 headers 就開始處理，SSE 必須
                response = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                var statusCode = (int)response.StatusCode;

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("{Provider} API 回傳 {StatusCode}，response body：{Body}", providerId, statusCode, errorBody);

                    // 解析 error code 以提供更精確的提示
                    string? errorCode = null;
                    try
                    {
                        using var doc = JsonDocument.Parse(errorBody);
                        var root = doc.RootElement;
                        // 各提供商格式：{"error":{"code":"..."}} 或 {"message":"...","code":"..."}
                        errorCode = root.TryGetProperty("error", out var errEl)
                            ? (errEl.TryGetProperty("code", out var c) ? c.GetString() : null)
                            : (root.TryGetProperty("code", out var c2) ? c2.GetString() : null);
                    }
                    catch { /* 非 JSON 格式不處理 */ }

                    friendlyError = statusCode switch
                    {
                        401 => $"{providerDisplay} API Key 無效或已過期，請至設定頁更新 Key",
                        403 => $"沒有存取 {providerDisplay} 的權限，請確認帳號方案或 API Key 權限",
                        404 => $"{providerDisplay} 找不到指定的模型，請切換其他模型",
                        413 => $"此對話的訊息累積過長，已超過 {providerDisplay} 的 token 上限，請開新對話繼續",
                        429 => null, // 由 isRateLimit 處理
                        500 or 502 => $"{providerDisplay} 服務發生內部錯誤，請稍後再試",
                        503 => $"{providerDisplay} 服務暫時不可用，請稍後再試",
                        _ when errorCode is "model_decommissioned" or "model_not_found" =>
                            $"{providerDisplay} 的模型已下架或不存在，請切換其他模型",
                        _ => $"AI 服務暫時無法使用（{statusCode}）"
                    };

                    if (statusCode == 429)
                        isRateLimit = true;

                    callError = new HttpRequestException($"HTTP {statusCode}");
                }
            }
            catch (TaskCanceledException)
            {
                // 超時：timeout 比一般 OperationCanceledException 更常見且有明確意義
                _logger.LogWarning("{Provider} API 請求逾時", providerId);
                friendlyError = $"{providerDisplay} 回應逾時，請稍後再試或切換其他模型";
                callError = new TimeoutException();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "{Provider} API 呼叫失敗", providerId);
                friendlyError = "網路連線失敗，請確認網路狀態後再試";
                callError = ex;
            }

            if (callError != null)
            {
                if (isRateLimit)
                    yield return new ChatStreamChunk { Type = "rate_limit", Content = $"{providerDisplay} 免費額度已達上限，請稍後再試或切換其他模型" };
                else
                    yield return new ChatStreamChunk { Type = "error", Content = friendlyError ?? "AI 服務暫時無法使用" };
                yield break;
            }

            using var stream = await response!.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new System.IO.StreamReader(stream);

            // 收集最新 usage chunk，在 [DONE] 時統一 yield 一次 done
            TokenUsage? collectedUsage = null;

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

                var data = line["data: ".Length..];
                if (data == "[DONE]")
                {
                    yield return new ChatStreamChunk { Type = "done", Usage = collectedUsage ?? new TokenUsage() };
                    yield break;
                }

                using var doc = JsonDocument.Parse(data);
                var root = doc.RootElement;

                // 記錄最新 usage，不立即 yield（等 [DONE] 統一發送）
                if (root.TryGetProperty("usage", out var usage) && usage.ValueKind != JsonValueKind.Null)
                {
                    collectedUsage = new TokenUsage
                    {
                        TokensIn = usage.GetProperty("prompt_tokens").GetInt32(),
                        TokensOut = usage.GetProperty("completion_tokens").GetInt32()
                    };
                }

                if (!root.TryGetProperty("choices", out var choicesEl) || choicesEl.GetArrayLength() == 0)
                    continue;

                var delta = choicesEl[0].GetProperty("delta");
                if (delta.TryGetProperty("content", out var contentEl) &&
                    contentEl.ValueKind == JsonValueKind.String)
                {
                    yield return new ChatStreamChunk { Type = "delta", Content = contentEl.GetString() };
                }
            }
        }
    }
}
