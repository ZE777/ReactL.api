# AI 串接錯誤處理指南

## 錯誤處理位置

`ReactL.api/Services/Ai/OpenAiService.cs` — `CallOpenAiStreamAsync` 方法

所有 AI 提供商的 HTTP 錯誤在此統一處理，轉換為 SSE chunk 回傳前端。

---

## 錯誤回傳格式

後端透過 SSE 串流回傳以下兩種錯誤 chunk：

| chunk type | 觸發時機 | 前端行為 |
|------------|---------|---------|
| `error` | 一般錯誤（4xx、5xx、逾時、網路斷線） | Toast 顯示 `content` 訊息 |
| `rate_limit` | HTTP 429 免費額度耗盡 | Toast 顯示 `content` 訊息 |

---

## HTTP 狀態對應表

| HTTP 狀態 | 實際意義 | 前端顯示訊息 |
|-----------|---------|------------|
| 401 | API Key 無效或過期 | `{Provider} API Key 無效或已過期，請至設定頁更新 Key` |
| 403 | 帳號無權限 | `沒有存取 {Provider} 的權限，請確認帳號方案或 API Key 權限` |
| 404 | 模型 ID 不存在 | `{Provider} 找不到指定的模型，請切換其他模型` |
| 413 | 對話 token 超過上限 | `此對話的訊息累積過長，已超過 {Provider} 的 token 上限，請開新對話繼續` |
| 429 | 免費額度耗盡 | `{Provider} 免費額度已達上限，請稍後再試或切換其他模型` |
| 400 `model_decommissioned` | 模型已下架 | `{Provider} 的模型已下架或不存在，請切換其他模型` |
| 400 `model_not_found` | 模型 ID 設定錯誤 | `{Provider} 的模型已下架或不存在，請切換其他模型` |
| 500 / 502 | 提供商內部錯誤 | `{Provider} 服務發生內部錯誤，請稍後再試` |
| 503 | 提供商服務中斷 | `{Provider} 服務暫時不可用，請稍後再試` |
| 網路逾時 | `TaskCanceledException` | `{Provider} 回應逾時，請稍後再試或切換其他模型` |
| 網路斷線 | 其他未預期例外 | `網路連線失敗，請確認網路狀態後再試` |
| 其他 4xx | 未分類錯誤 | `AI 服務暫時無法使用（{statusCode}）` |

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