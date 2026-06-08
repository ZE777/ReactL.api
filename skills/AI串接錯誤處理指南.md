# AI 串接錯誤處理指南

## 錯誤處理位置（單一事實來源）

`ReactL.api/Common/Ai/AiError.cs` — `AiErrorClassifier`

**所有「狀態碼／例外 → 友善訊息」的對應只存在這裡**，串流與非串流、四個載體共用同一套：

- `AiErrorClassifier.Classify(statusCode, providerDisplay, errorCode?)` → 回傳 `AiError`
- `AiErrorClassifier.Timeout(providerDisplay)` / `AiErrorClassifier.Network()`
- `AiError` 含 `Kind`（`AiErrorKind` 列舉）、`FriendlyMessage`、`Retryable`，並衍生 `ChunkType`（限流→`rate_limit`，其餘→`error`）

呼叫端：
- **串流**（`OpenAiService.CallOpenAiStreamAsync`，供前台公開聊天 + 後台聊天室）：依 `AiError.ChunkType` 回傳對應 SSE chunk。
- **非串流**（`OpenAiService.PostChatCompletionWithRetryAsync` → `CompleteWithUsageAsync` / `CompleteAsync` / `CompleteWithToolsAsync`）：丟出 `UpstreamAiException(aiError)`，該例外攜帶 `Kind` / `ChunkType`。
- **LINE / Discord Bot**：`catch (UpstreamAiException)` → 直接把 `ex.Message`（友善訊息）回給使用者。

> ⚠️ 修改錯誤訊息或新增錯誤分類，**只改 `AiErrorClassifier`**，所有載體自動同步；不要再在各處寫 switch。

---

## 錯誤回傳格式

| 載體 | 錯誤呈現方式 |
|------|------------|
| 前台公開聊天 / 後台聊天室（SSE） | `error` 或 `rate_limit` chunk，前端 Toast / 泡泡顯示 `content` |
| LINE / Discord Bot | 以一般訊息回覆 `UpstreamAiException.Message` |

SSE chunk 類型：

| chunk type | 觸發時機 | 前端行為 |
|------------|---------|---------|
| `error` | 一般錯誤（4xx、5xx、逾時、網路斷線） | 顯示 `content` 訊息 |
| `rate_limit` | HTTP 429 免費額度耗盡 | 顯示 `content` 訊息（前端可特別提示） |

---

## 狀態 / 例外對應表（以 `AiErrorClassifier` 為準）

| 來源 | `AiErrorKind` | Retryable | 友善訊息 |
|------|---------------|-----------|---------|
| 400 | `BadRequest` | 否 | `{Provider} 無法處理此請求（多為模型產生的指令格式有誤），請重試或改用較強的模型` |
| 401 | `Auth` | 否 | `{Provider} API Key 無效或已過期，請至設定頁更新 Key` |
| 403 | `Forbidden` | 否 | `沒有存取 {Provider} 的權限，請確認帳號方案或 API Key 權限` |
| 404 | `ModelNotFound` | 否 | `{Provider} 找不到指定的模型，請切換其他模型` |
| 408 | `Timeout` | 是 | `{Provider} 回應逾時，請稍後再試` |
| 413 | `ContentTooLong` | 否 | `內容過長，已超過 {Provider} 的 token 上限，請縮短內容或開新對話再試` |
| 429 | `RateLimit` | 是 | `{Provider} 免費額度已達上限，請稍後再試或切換其他模型` |
| `model_decommissioned` / `model_not_found`（任何狀態碼帶此 code） | `ModelNotFound` | 否 | `{Provider} 的模型已下架或不存在，請切換其他模型` |
| ≥ 500 | `ServerError` | 是 | `{Provider} 服務暫時不可用，請稍後再試` |
| `TaskCanceledException`（逾時） | `Timeout` | 是 | `{Provider} 回應逾時，請稍後再試或切換其他模型` |
| `HttpRequestException`（連線失敗） | `Network` | 是 | `AI 服務連線失敗，請稍後再試` |
| 其他未分類狀態碼 | `Unknown` | 否 | `AI 服務暫時無法使用（{statusCode}）` |

> `{Provider}` 為該次實際呼叫的供應商 DisplayName（依當下模型解析）。LINE / Discord 皆使用 **該 Bot 設定的模型** 解析供應商，故訊息會顯示對應供應商名稱。
> Retryable=是 者由 `PostChatCompletionWithRetryAsync` 依 `IsTransientStatus`（429/408/5xx）自動重試；Retry-After 過長（>10s）則放棄重試直接回報。

---

## 各提供商特殊行為

### stream_options 相容性

`stream_options: { include_usage: true }` 只有 Groq 支援，其他提供商傳此欄位會回傳 400。

在 `AiProviderConfig` 以 `SupportsStreamUsage` 旗標控制，目前設定：

| 提供商 | SupportsStreamUsage |
|--------|---------------------|
| Groq | `true` |
| Mistral | `false`（預設） |
| Cerebras | `false`（預設） |
| SambaNova | `false`（預設） |

### 已知棄用模型（截至 2026-06）

| 提供商 | 棄用 ID | 錯誤碼 |
|--------|--------|--------|
| Groq | `deepseek-r1-distill-llama-70b` | 400 `model_decommissioned` |
| Groq | `deepseek-r1-distill-qwen-32b` | 400 `model_decommissioned` |
| SambaNova | `Qwen2.5-72B-Instruct` | 410 |
| SambaNova | `Llama-4-Scout-17B-16E-Instruct` | 410 |
| SambaNova | `Meta-Llama-3.1-8B-Instruct` | 410 |
| Cerebras | `llama-3.3-70b` | 404 |
| Cerebras | `llama3.1-70b` | 404 |

---

## 查詢各提供商現有模型

各提供商均支援 OpenAI 標準的 `/models` 端點：

```powershell
# Groq
Invoke-RestMethod -Uri "https://api.groq.com/openai/v1/models" `
  -Headers @{ Authorization = "Bearer {key}" } | ConvertTo-Json -Depth 3

# Cerebras
Invoke-RestMethod -Uri "https://api.cerebras.ai/v1/models" `
  -Headers @{ Authorization = "Bearer {key}" } | ConvertTo-Json -Depth 3

# SambaNova
Invoke-RestMethod -Uri "https://api.sambanova.ai/v1/models" `
  -Headers @{ Authorization = "Bearer {key}" } | ConvertTo-Json -Depth 3
```

取得有效 ID 後更新 `appsettings.json` 的 `AiSettings.Providers[].Models` 並重啟後端。

---

## 新增提供商檢查清單

1. 在 `appsettings.json` 的 `AiSettings.Providers` 加入設定
2. 確認是否支援 `stream_options`，設定 `SupportsStreamUsage`
3. 在 `appsettings.Development.json` 的 `AiSettings.ProviderKeys` 加入 API Key
4. 呼叫 `/models` 端點確認有效模型 ID