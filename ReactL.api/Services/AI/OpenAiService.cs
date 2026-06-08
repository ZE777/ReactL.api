using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReactL.api.Common.Ai;
using ReactL.api.Common.Constants;
using ReactL.api.Common.Exceptions;
using ReactL.api.Common.Settings;
using ReactL.api.Data;
using ReactL.api.DTOs.Ai;
using ReactL.api.DTOs.Requests.Ai;
using ReactL.api.DTOs.Responses.Ai;
using ReactL.api.Models.Conversations;
using ReactL.api.Models.Stats;
using ReactL.api.Services.Access;
using ReactL.api.Services.PublicChat;
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
        private readonly IAiKeyService _aiKeys;
        private readonly IAccessCodeService _accessCodes;
        private readonly IPublicChatLogService _publicChatLogs;
        private readonly ILogger<OpenAiService> _logger;

        public OpenAiService(
            AppDbContext db,
            IOptions<AiSettings> aiSettings,
            IHttpClientFactory httpClientFactory,
            IAiKeyService aiKeys,
            IAccessCodeService accessCodes,
            IPublicChatLogService publicChatLogs,
            ILogger<OpenAiService> logger)
        {
            _db = db;
            _aiSettings = aiSettings.Value;
            _httpClientFactory = httpClientFactory;
            _aiKeys = aiKeys;
            _accessCodes = accessCodes;
            _publicChatLogs = publicChatLogs;
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

            // C 方案後端攔截：後台聊天必須使用使用者自帶金鑰，不 fallback 系統 key
            // 在寫入任何訊息 / 呼叫 AI 前先擋下，避免白嫖系統額度
            var authKey = await _aiKeys.ResolveKeyAsync(userId, providerId, allowSystemFallback: false, cancellationToken);
            if (string.IsNullOrEmpty(authKey))
            {
                yield return new ChatStreamChunk { Type = "error", Content = $"你尚未設定 {providerDisplay} 的 API 金鑰，請至「AI 金鑰」頁面新增後再使用" };
                yield break;
            }

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

            await foreach (var chunk in CallOpenAiStreamAsync(providerId, modelName, providerDisplay, messages, supportsStreamUsage, authKey, cancellationToken))
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

                // 累加每日 Token 用量統計（UPSERT）
                if (tokensIn > 0 || tokensOut > 0)
                {
                    var today = DateOnly.FromDateTime(DateTime.Now);
                    var stat = await _db.TokenUsageStats.FirstOrDefaultAsync(
                        s => s.UserId == userId && s.Date == today && s.ModelType == rawModel && s.Source == TokenSource.Admin,
                        CancellationToken.None);

                    if (stat is null)
                    {
                        _db.TokenUsageStats.Add(new TokenUsageStat
                        {
                            UserId    = userId,
                            Date      = today,
                            ModelType = rawModel,
                            Source    = TokenSource.Admin,
                            TokensIn  = tokensIn,
                            TokensOut = tokensOut,
                            RequestCount = 1,
                        });
                    }
                    else
                    {
                        stat.TokensIn    += tokensIn;
                        stat.TokensOut   += tokensOut;
                        stat.RequestCount += 1;
                    }
                    await _db.SaveChangesAsync(CancellationToken.None);
                }
            }

            // C-05：第一輪對話完成，呼叫 AI 產生標題並寫入 DB
            // yield return 不能放在 try-catch 內，改用 generatedTitle 暫存後在外部 yield
            string? generatedTitle = null;
            if (doneChunk != null && isFirstExchange && fullContent.Length > 0)
            {
                try
                {
                    const string titleSystem =
                        "你是一個對話標題生成器。根據使用者的第一則訊息，生成一個簡潔的繁體中文標題（10~20 字）。" +
                        "精確描述對話主題，只輸出標題文字，不要加引號、序號或任何說明。";
                    var titleUser = $"使用者訊息：{request.UserMessage}";

                    // OpenAiService 本身即 IAiService 實作，直接呼叫自身的 CompleteAsync
                    var newTitle = (await CompleteAsync(titleSystem, titleUser, userId))
                        .Trim().Trim('"').Trim('「').Trim('」');

                    if (!string.IsNullOrWhiteSpace(newTitle))
                    {
                        // 再次確認標題仍為預設，避免使用者在串流期間手動改名被覆蓋
                        var convToUpdate = await _db.Conversations.FindAsync(conversation.Id);
                        if (convToUpdate != null && convToUpdate.Title == "新對話")
                        {
                            convToUpdate.Title = newTitle;
                            await _db.SaveChangesAsync(CancellationToken.None);
                            generatedTitle = newTitle;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "C-05 自動產生對話標題失敗，跳過");
                }
            }

            // title_updated 在 done 之前 yield，確保前端 invalidateQueries 取到已更新的標題
            if (generatedTitle != null)
                yield return new ChatStreamChunk { Type = "title_updated", Content = generatedTitle };

            // 所有 DB 操作完成後才 yield done，確保前端 invalidateQueries 取到完整資料
            if (doneChunk != null)
                yield return doneChunk;
        }

        public async IAsyncEnumerable<ChatStreamChunk> PublicChatStreamAsync(
            PublicChatRequest request,
            string? accessCode,
            string? sessionId,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            // 存取碼閘門：驗證碼有效性 + 全域/每碼每日 token 配額；不通過則回單一 chunk 後結束
            var gate = await _accessCodes.ValidateForChatAsync(accessCode, cancellationToken);
            if (!gate.Allowed)
            {
                yield return new ChatStreamChunk { Type = gate.ChunkType, Content = gate.Message };
                yield break;
            }

            // 取得 Persona 的 System Prompt 與模型（僅允許 isBuiltin=true 的公開角色）
            string? systemPrompt = null;
            string? personaModel = null;
            if (request.PersonaId.HasValue)
            {
                var persona = await _db.Personas
                    .AsNoTracking()
                    .Where(p => p.Id == request.PersonaId.Value && p.IsBuiltin)
                    .Select(p => new { p.SystemPrompt, p.ModelType })
                    .FirstOrDefaultAsync(cancellationToken);
                systemPrompt = persona?.SystemPrompt;
                personaModel = persona?.ModelType;
            }

            // 模型以角色設定為準（後端權威），角色未設定才退回請求帶入值
            var effectiveModel = !string.IsNullOrWhiteSpace(personaModel) ? personaModel! : request.ModelType;

            // 記錄使用者訊息（供後台 Admin 監控前台聊天）；best-effort，不影響回應
            await _publicChatLogs.LogMessageAsync(
                sessionId, gate.AccessCodeId, accessCode, "user", request.UserMessage,
                effectiveModel, request.PersonaId, 0, 0, cancellationToken);

            var (providerId, modelName) = ParseModelType(effectiveModel);
            var providerConfig = _aiSettings.Providers.FirstOrDefault(p => p.Id == providerId);
            var providerDisplay = providerConfig?.DisplayName ?? providerId;
            var supportsStreamUsage = providerConfig?.SupportsStreamUsage ?? false;

            // 前端傳入完整歷史，組裝 messages 陣列
            var messages = new List<object>();
            if (!string.IsNullOrEmpty(systemPrompt))
                messages.Add(new { role = "system", content = systemPrompt });

            foreach (var m in request.Messages)
                messages.Add(new { role = m.Role, content = m.Content });

            messages.Add(new { role = "user", content = request.UserMessage });

            // 公開聊天室無登入使用者 → ownerUserId 為 null，允許走系統預設金鑰（前台不受 C 方案影響）
            var authKey = await _aiKeys.ResolveKeyAsync(null, providerId, allowSystemFallback: true, cancellationToken);

            var tokensIn = 0;
            var tokensOut = 0;
            // 累積 AI 回應全文，串流結束後寫入監控記錄
            var assistantContent = new StringBuilder();
            await foreach (var chunk in CallOpenAiStreamAsync(providerId, modelName, providerDisplay, messages, supportsStreamUsage, authKey, cancellationToken))
            {
                if (chunk.Type == "delta" && chunk.Content != null)
                {
                    assistantContent.Append(chunk.Content);
                }
                else if (chunk.Type == "done" && chunk.Usage != null)
                {
                    tokensIn = chunk.Usage.TokensIn;
                    tokensOut = chunk.Usage.TokensOut;
                }
                yield return chunk;
            }

            // 串流結束後記錄用量（per-code 每日配額 + 全域系統用量）；best-effort，不影響回應
            await _accessCodes.RecordUsageAsync(gate.AccessCodeId, effectiveModel, tokensIn, tokensOut, cancellationToken);

            // 記錄 AI 回應（供後台 Admin 監控前台聊天）；best-effort，不影響回應
            if (assistantContent.Length > 0)
            {
                await _publicChatLogs.LogMessageAsync(
                    sessionId, gate.AccessCodeId, accessCode, "assistant", assistantContent.ToString(),
                    effectiveModel, request.PersonaId, tokensIn, tokensOut, cancellationToken);
            }
        }

        public async Task<string> CompleteAsync(
            string systemPrompt,
            string userPrompt,
            Guid? ownerUserId = null,
            CancellationToken cancellationToken = default,
            bool allowSystemFallback = true)
        {
            var (reply, _, _) = await CompleteWithUsageAsync(systemPrompt, userPrompt, ownerUserId, cancellationToken, allowSystemFallback);
            return reply;
        }

        public async Task<(string Reply, int TokensIn, int TokensOut)> CompleteWithUsageAsync(
            string systemPrompt,
            string userPrompt,
            Guid? ownerUserId = null,
            CancellationToken cancellationToken = default,
            bool allowSystemFallback = true,
            string? modelType = null)
        {
            // 傳入 modelType（如 LINE Bot 的設定模型）則用之；否則用系統預設模型
            var (providerId, modelName) = ParseModelType(
                string.IsNullOrWhiteSpace(modelType) ? _aiSettings.DefaultModel : modelType);
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

            var providerDisplay = ResolveProviderDisplay(providerId);
            var authKey = await _aiKeys.ResolveKeyAsync(ownerUserId, providerId, allowSystemFallback, cancellationToken);
            // 後端攔截：後台來源（allowSystemFallback=false）若無自帶金鑰，直接擋下而非借用系統 key
            if (!allowSystemFallback && string.IsNullOrEmpty(authKey))
                throw new ForbiddenException($"你尚未設定 {providerDisplay} 的 API 金鑰，請至「AI 金鑰」頁面新增後再使用");
            var client = _httpClientFactory.CreateClient($"ai:{providerId}");

            // 非串流呼叫加入自動重試（429/408/5xx/逾時/連線失敗），失敗時丟出帶友善訊息的例外
            using var response = await PostChatCompletionWithRetryAsync(
                client, providerId, providerDisplay, body, authKey, cancellationToken);

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            var reply = root
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            // OpenAI 相容格式：prompt_tokens / completion_tokens
            int tokensIn = 0, tokensOut = 0;
            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var pt)) tokensIn = pt.GetInt32();
                if (usage.TryGetProperty("completion_tokens", out var ct)) tokensOut = ct.GetInt32();
            }

            return (reply, tokensIn, tokensOut);
        }

        public async Task<AiToolResult> CompleteWithToolsAsync(
            string systemPrompt,
            string userPrompt,
            IReadOnlyList<AiFunctionTool> tools,
            string modelType,
            Guid? ownerUserId = null,
            CancellationToken cancellationToken = default,
            bool allowSystemFallback = true)
        {
            var (providerId, modelName) = ParseModelType(modelType);
            var messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            };

            // 包成 OpenAI tools 格式
            var toolsPayload = tools.Select(t => new
            {
                type = "function",
                function = new { name = t.Name, description = t.Description, parameters = t.Parameters }
            }).ToArray();

            // Discord 工具路徑用獨立的較小上限（AiSettings.ToolCallMaxTokens，預設 2048）：
            // 該路徑常用 TPM 較低的小模型，且 Discord 訊息會截斷，較小值可避免請求超過 TPM 而 413。
            var body = new
            {
                model = modelName,
                messages,
                tools = toolsPayload,
                tool_choice = "auto",
                max_tokens = _aiSettings.ToolCallMaxTokens,
                stream = false
            };

            var providerDisplay = ResolveProviderDisplay(providerId);
            var authKey = await _aiKeys.ResolveKeyAsync(ownerUserId, providerId, allowSystemFallback, cancellationToken);
            if (!allowSystemFallback && string.IsNullOrEmpty(authKey))
                throw new ForbiddenException($"你尚未設定 {providerDisplay} 的 API 金鑰，請至「AI 金鑰」頁面新增後再使用");
            var client = _httpClientFactory.CreateClient($"ai:{providerId}");

            using var response = await PostChatCompletionWithRetryAsync(
                client, providerId, providerDisplay, body, authKey, cancellationToken);

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;
            var message = root.GetProperty("choices")[0].GetProperty("message");

            var result = new AiToolResult();

            // 有 tool_calls 代表模型決定呼叫工具；否則取純文字 content
            if (message.TryGetProperty("tool_calls", out var toolCalls)
                && toolCalls.ValueKind == JsonValueKind.Array && toolCalls.GetArrayLength() > 0)
            {
                foreach (var call in toolCalls.EnumerateArray())
                {
                    if (!call.TryGetProperty("function", out var fn)) continue;
                    result.ToolCalls.Add(new AiToolCall
                    {
                        Name = fn.TryGetProperty("name", out var n) ? (n.GetString() ?? string.Empty) : string.Empty,
                        ArgumentsJson = fn.TryGetProperty("arguments", out var a) ? (a.GetString() ?? "{}") : "{}"
                    });
                }
            }
            else
            {
                result.TextReply = message.TryGetProperty("content", out var c) ? c.GetString() : null;
            }

            if (root.TryGetProperty("usage", out var usage))
            {
                if (usage.TryGetProperty("prompt_tokens", out var pt)) result.TokensIn = pt.GetInt32();
                if (usage.TryGetProperty("completion_tokens", out var ct)) result.TokensOut = ct.GetInt32();
            }

            return result;
        }

        // ── 私有輔助方法 ──────────────────────────────────────────────────────

        /// <summary>取得提供商顯示名稱，找不到時回傳 providerId 本身</summary>
        private string ResolveProviderDisplay(string providerId) =>
            _aiSettings.Providers.FirstOrDefault(p => p.Id == providerId)?.DisplayName ?? providerId;

        private static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max];

        /// <summary>重試等待上限；超過此值的 Retry-After（如每日額度）視為不值得等，直接快速失敗</summary>
        private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(10);

        /// <summary>
        /// 對 chat/completions 發送非串流請求，並對暫時性失敗自動重試（指數退避，尊重 Retry-After）。
        /// 重試耗盡或遇到不可重試的 4xx 時，丟出 <see cref="UpstreamAiException"/>（攜帶友善訊息）。
        /// </summary>
        private async Task<HttpResponseMessage> PostChatCompletionWithRetryAsync(
            HttpClient client,
            string providerId,
            string providerDisplay,
            object body,
            string? authKey,
            CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(body);
            var maxAttempts = Math.Max(1, _aiSettings.MaxRetryCount);

            for (var attempt = 1; ; attempt++)
            {
                int statusCode;
                AiError aiError;
                bool retryable;
                TimeSpan? retryAfter = null;

                try
                {
                    // StringContent 不可跨請求重用，每次 attempt 都重新建立
                    var content = new StringContent(json, Encoding.UTF8);
                    content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                    using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "chat/completions") { Content = content };
                    // 使用解析後的金鑰；request 層 header 優先於 HttpClient 啟動時設定的預設 Key
                    if (!string.IsNullOrEmpty(authKey))
                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authKey);

                    var response = await client.SendAsync(requestMessage, cancellationToken);
                    if (response.IsSuccessStatusCode)
                        return response;

                    statusCode = (int)response.StatusCode;
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    retryAfter = response.Headers.RetryAfter?.Delta;
                    response.Dispose();

                    // error code 有助於辨識「模型下架」等狀況
                    string? errorCode = null;
                    try
                    {
                        using var doc = JsonDocument.Parse(errorBody);
                        var rootEl = doc.RootElement;
                        errorCode = rootEl.TryGetProperty("error", out var errEl)
                            ? (errEl.TryGetProperty("code", out var c) ? c.GetString() : null)
                            : (rootEl.TryGetProperty("code", out var c2) ? c2.GetString() : null);
                    }
                    catch { /* 非 JSON 格式不處理 */ }

                    aiError = AiErrorClassifier.Classify(statusCode, providerDisplay, errorCode);
                    retryable = aiError.Retryable;
                    _logger.LogWarning("{Provider} 非串流回傳 {StatusCode}（嘗試 {Attempt}/{Max}）：{Body}",
                        providerId, statusCode, attempt, maxAttempts, Truncate(errorBody, 300));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // 用戶端主動取消，交給 ExceptionMiddleware 對應 499，不視為錯誤
                    throw;
                }
                catch (TaskCanceledException)
                {
                    statusCode = 504;
                    aiError = AiErrorClassifier.Timeout(providerDisplay);
                    retryable = true;
                    _logger.LogWarning("{Provider} 非串流請求逾時（嘗試 {Attempt}/{Max}）", providerId, attempt, maxAttempts);
                }
                catch (HttpRequestException ex)
                {
                    statusCode = 502;
                    aiError = AiErrorClassifier.Network();
                    retryable = true;
                    _logger.LogWarning(ex, "{Provider} 非串流連線失敗（嘗試 {Attempt}/{Max}）", providerId, attempt, maxAttempts);
                }

                // 不可重試，或已是最後一次嘗試 → 丟出友善例外
                if (!retryable || attempt >= maxAttempts)
                {
                    _logger.LogError("{Provider} 非串流呼叫最終失敗 StatusCode={StatusCode} 已嘗試 {Attempt} 次", providerId, statusCode, attempt);
                    throw new UpstreamAiException(aiError);
                }

                // 退避：優先採用 Retry-After，否則指數退避（0.4s、0.8s、1.6s…）
                var delay = retryAfter ?? TimeSpan.FromMilliseconds(400 * Math.Pow(2, attempt - 1));

                // Retry-After 過長（如每日額度可能要等數十分鐘）→ 重試無意義，且會拖過
                // Discord 互動 15 分鐘時限導致「思考中」卡死。直接快速失敗回報友善訊息。
                if (delay > MaxRetryDelay)
                {
                    _logger.LogWarning("{Provider} Retry-After {DelaySec}s 過長，放棄重試直接回報（{StatusCode}）",
                        providerId, (int)delay.TotalSeconds, statusCode);
                    throw new UpstreamAiException(aiError);
                }

                _logger.LogInformation("{Provider} 將於 {DelayMs}ms 後重試（下一次 {Next}/{Max}）",
                    providerId, (int)delay.TotalMilliseconds, attempt + 1, maxAttempts);
                await Task.Delay(delay, cancellationToken);
            }
        }

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
            string? authKey,
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

            // 使用解析後的金鑰（使用者自帶 → 系統預設 → appsettings）；request 層 header 優先於 HttpClient 預設
            if (!string.IsNullOrEmpty(authKey))
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authKey);

            // yield 不能在 catch 內使用，改用旗標與分類結果傳遞到 yield 區段
            HttpResponseMessage? response = null;
            Exception? callError = null;
            AiError? aiError = null;

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

                    aiError = AiErrorClassifier.Classify(statusCode, providerDisplay, errorCode);
                    callError = new HttpRequestException($"HTTP {statusCode}");
                }
            }
            catch (TaskCanceledException)
            {
                // 超時：timeout 比一般 OperationCanceledException 更常見且有明確意義
                _logger.LogWarning("{Provider} API 請求逾時", providerId);
                aiError = AiErrorClassifier.Timeout(providerDisplay);
                callError = new TimeoutException();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "{Provider} API 呼叫失敗", providerId);
                aiError = AiErrorClassifier.Network();
                callError = ex;
            }

            if (callError != null)
            {
                // 限流 → rate_limit chunk；其餘 → error chunk（分類由 AiErrorClassifier 統一決定）
                var e = aiError ?? AiErrorClassifier.Network();
                yield return new ChatStreamChunk { Type = e.ChunkType, Content = e.FriendlyMessage };
                yield break;
            }

            using var stream = await response!.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new System.IO.StreamReader(stream);

            // 收集最新 usage chunk，在 [DONE] 時統一 yield 一次 done
            TokenUsage? collectedUsage = null;
            string? finishReason = null;

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

                var data = line["data: ".Length..];
                if (data == "[DONE]")
                {
                    // 我們自己的 max_tokens 限制導致回應被截斷
                    if (finishReason == "length")
                        yield return new ChatStreamChunk
                        {
                            Type = "truncated",
                            Content = $"回應已達 {_aiSettings.MaxTokens:N0} token 上限而中斷，如需完整回應可至後端設定調高 MaxTokens"
                        };

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

                var choice = choicesEl[0];

                // 記錄 finish_reason（最終 chunk 才會非 null）
                if (choice.TryGetProperty("finish_reason", out var fr) && fr.ValueKind == JsonValueKind.String)
                    finishReason = fr.GetString();

                var delta = choice.GetProperty("delta");
                if (delta.TryGetProperty("content", out var contentEl) &&
                    contentEl.ValueKind == JsonValueKind.String)
                {
                    yield return new ChatStreamChunk { Type = "delta", Content = contentEl.GetString() };
                }
            }
        }
    }
}
