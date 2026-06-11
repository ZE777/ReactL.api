# ReactL.api 後端技術總覽

> 最後更新：2026-06-09
> 專案：ReactL Prompt Studio（後台 API）
> 框架：ASP.NET Core 8.0 / .NET 8
> Schema 管理：SqlScripts 版本化腳本（EF Migration 已於 2026-06-05 移除）
> 技術選型概要見 `README.md` 的「技術應用概要」；本文件為實作層細節。前端對應文件：`ReactL`（前端 repo）`docs/前端實作細節.md`。

---

## 目錄

1. [技術棧一覽](#1-技術棧一覽)
2. [專案架構](#2-專案架構)
3. [認證與授權](#3-認證與授權)
4. [資料庫設計](#4-資料庫設計)
5. [統一回應格式與錯誤處理](#5-統一回應格式與錯誤處理)
6. [AI 多提供商整合](#6-ai-多提供商整合)
7. [資料加密](#7-資料加密)
8. [DTO 與 Entity 設計規範](#8-dto-與-entity-設計規範)
9. [日誌與監控](#9-日誌與監控)
10. [CORS 設定](#10-cors-設定)
11. [敏感資訊管理](#11-敏感資訊管理)

---

## 1. 技術棧一覽

| 層級 | 技術 | 說明 |
|------|------|------|
| 語言 | C# 12（.NET 8） | Nullable reference types、Implicit using |
| Web 框架 | ASP.NET Core 8 | Controllers + Middleware 架構 |
| ORM | Entity Framework Core 8 | Code First + Fluent API |
| 資料庫 | SQL Server | LocalDB（開發）/ SQL Server（生產）|
| 認證 | JWT Bearer | HMAC-SHA256、ClockSkew 1 分鐘 |
| 密碼 | BCrypt.Net-Next 4 | 雙向驗證，不可逆雜湊 |
| 加密 | AES-256-CBC | Bot Token / Channel Secret 儲存 |
| 日誌 | Serilog | Console + 每日滾動檔案（30 天保留）|
| API 文件 | Swagger / OpenAPI 3 | XML 文件 + JWT 登入框 |
| AI 整合 | OpenAI-Compatible API | 多提供商、SSE 串流 |
| 健康檢查 | EF Core HealthCheck | GET /health |

---

## 2. 專案架構

採用 **Controller → Service → DbContext** 三層架構，無獨立 Repository 層。  
Service 回傳 **Domain 物件**，Controller 再 map 成 **Response DTO**。

```
Controllers/          路由入口，只做參數驗證與 Domain → Response DTO 轉換
├── Admin/            後台登入使用者的 API（需 JWT）
│   ├── Ai/           SSE 串流對話、可用模型清單
│   ├── AiKeys/       使用者 AI 金鑰管理（BYOK）
│   ├── Auth/
│   ├── BotBindings/
│   ├── Conversations/
│   ├── Monitor/      外部對話監控 + Token 統計（messages / conversations / stats/tokens）
│   ├── Personas/
│   ├── PromptTemplates/
│   └── Users/
├── Web/              公開前台 API（無需登入或僅需 ShareSlug）
│   ├── Chat/         PublicChatController（公開聊天 SSE，不寫入 DB）
│   ├── Conversations/  SharedConversationsController（以 ShareSlug 讀取）
│   └── Personas/     PublicPersonasController（公開可選角色）
└── Webhooks/         外部平台回呼，免 JWT、自帶簽章驗證
    ├── LineWebhookController     POST /webhooks/line/{botId}
    └── DiscordWebhookController  POST /webhooks/discord/{botId}
（Base / Chat / Stats 為保留資料夾，目前無 Controller）

Services/             業務邏輯，直接注入 AppDbContext，Entity → Domain 投影
├── Auth/
├── AI/               OpenAiService（IAiService）、AiKeyService（IAiKeyService，BYOK 解析 + 啟動種子）
├── BotBindings/
├── Conversations/
├── Monitor/
├── Personas/
├── PromptTemplates/
├── Users/
└── Webhooks/         LineWebhookService、DiscordWebhookService、DiscordCommandService、LineCredentialService

Domain/               業務物件層，Service 回傳型別，不含敏感欄位
├── Auth/             AuthResultDomain, UserDomain
├── BotBindings/      BotBindingDomain
├── Conversations/    ConversationDomain, MessageDomain
├── Monitor/          ExternalMessageDomain
├── Personas/         PersonaDomain, PersonaVersionDomain
└── PromptTemplates/  PromptTemplateDomain

Data/
├── AppDbContext.cs       全部 Entity 配置集中於 OnModelCreating
└── Configurations/       保留資料夾（目前 Fluent 配置內聯於 AppDbContext，未拆分）

Models/               Entity，唯一對應 SQL 資料表的位置
├── Base/             BaseEntity / AuditableEntity / SoftDeletableEntity
├── Ai/               AiKey（混合 BYOK 金鑰）
├── Auth/
├── BotBindings/
├── Conversations/
├── External/
├── Personas/
├── PromptTemplates/
└── Stats/

DTOs/                 HTTP 傳輸物件，分 Requests / Responses 兩個方向
├── Common/           ApiResponse<T>、PagedResponse<T>
├── Requests/         前端送入的輸入物件，含驗證 Attribute
│   ├── Ai/  AiKeys/  Auth/  BotBindings/  Conversations/
│   └── Monitor/  Personas/  PromptTemplates/  Users/  Webhooks/
└── Responses/        定義 API 回傳格式
    ├── Ai/  AiKeys/  Auth/  BotBindings/  Conversations/
    └── Monitor/  Personas/  PromptTemplates/  Users/

Middleware/
└── ExceptionMiddleware.cs   全域例外攔截 → ProblemDetails

Common/
├── Constants/        MessageRole、Platform、TokenSource、SystemUser
├── Exceptions/       AppException 體系（含 UpstreamAiException）
├── Extensions/       ClaimsPrincipalExtensions
├── Helpers/          AesEncryptionHelper
└── Settings/         強型別設定注入（含 AiProviderConfig）

SqlScripts/           Schema 版本化腳本（取代 EF Migration）
├── Init/             V001 初始結構
├── Migrations/       V002–V007 增量變更
└── Seed/             官方 Persona 種子
```

**完整資料流向**：

```
HTTP Request
    ↓ Request DTO（驗證輸入）
Controller 呼叫 Service
    ↓ IXxxService 介面
Service 查詢 AppDbContext
    ↓ Entity → Domain（過濾敏感欄位、加入 JOIN 計算欄位）
Controller 收到 Domain
    ↓ Domain → Response DTO（private static ToXxxResponse 方法）
ApiResponse<T>.Ok(dto) 回傳
```

**API 路由規範**：`/api/v1/{resource}`

**DI 生命週期**：
- Service：`Scoped`（每次請求）
- `AesEncryptionHelper`：`Singleton`（Key/IV 只載入一次）

---

## 3. 認證與授權

### JWT 設定（`Common/Settings/JwtSettings.cs`）

```csharp
public class JwtSettings {
    public string Issuer { get; set; }
    public string Audience { get; set; }
    public int ExpirationMinutes { get; set; }  // 預設 60 分鐘
    public string SecretKey { get; set; }       // HMAC-256，256 bit 以上
}
```

**Token 驗證參數**：
- ValidateIssuer / Audience / Lifetime / IssuerSigningKey：全部 true
- ClockSkew：1 分鐘（防止伺服器時間偏差）

### 認證流程（`Services/Auth/AuthService.cs`）

**登入**：
1. Email 不分大小寫查詢（防止帳號列舉繞過）
2. `BCrypt.Verify` 驗證密碼
3. 檢查 `IsActive` 狀態
4. 回傳 JWT，記錄 `LastLoginAt`
5. 統一錯誤訊息：「帳號或密碼錯誤」（防帳號列舉攻擊）

**ClaimsPrincipal 擴充**（`Common/Extensions/ClaimsPrincipalExtensions.cs`）：
- `GetUserId()` → Guid
- `GetEmail()` → string
- `GetRole()` → string
- `IsAdmin()` → bool

### 啟動種子：Admin 帳號與 AI 金鑰自動綁定（`Services/Auth/AdminSeeder.cs`）

API **每次啟動**時會在 `Program.cs` 開一個 Scope 依序執行兩支種子（皆冪等、失敗只記 Warning 不阻擋啟動）：

1. `IAiKeyService.SeedSystemKeysAsync` — 將 `appsettings` 的 AI 預設 Key 加密寫入系統用戶（`IsSystem = true`）。
2. `IAdminSeeder.SeedAsync` — 在資料庫**尚無任何 `Role = "Admin"` 帳號時**自動建立一支預設 Admin，並以系統預設 Key 自動綁定其 AI 金鑰。

> 順序不可顛倒：Admin 的 AI 金鑰是複製系統預設 Key，必須在系統 Key 種子之後執行。

**`AdminSeeder.SeedAsync` 行為**：

| 條件 | 行為 |
|------|------|
| `AdminSeedSettings.Enabled = false` | 直接略過，不建立 |
| 已存在任一 `Role = "Admin"` 帳號 | 略過，不重複種子 |
| 設定的 Email 已被其他帳號佔用 | **不覆寫**，僅記 Warning 提示手動將該帳號改為 Admin |
| 以上皆否 | 建立 Admin（`IsActive = true`、`MustChangePassword = true`），密碼以 BCrypt 雜湊 |

- 建立的 Admin 帶 `MustChangePassword = true`，**首次登入後強制改密**（預設密碼僅供首登）。
- 建立成功後立即呼叫私有的 `GrantSystemKeysToUserAsync`：
  - 撈出所有啟用中的系統預設 Key（`UserId = SystemUser.Id`、`IsSystem`、`IsActive`）。
  - 逐一以 `IsSystem = false` 複製綁定到 Admin（**直接重用系統 Key 的 AES 密文**，同一把金鑰、無需重新加密）。
  - 冪等：Admin 同供應商已存在金鑰則略過；若當下尚無任何系統 Key 則記 Warning 略過。
  - 目的：避免新 Admin 首登時因「尚未設定金鑰」被鎖在 AI 金鑰頁，開箱即可使用 AI 功能。

**設定（`Common/Settings/AdminSeedSettings.cs`，對應 `appsettings` 的 `AdminSeedSettings` section）**：

| 欄位 | 預設值 | 說明 |
|------|--------|------|
| `Enabled` | `true` | 是否啟用自動種子 Admin |
| `Email` | `admin@reactl.local` | 種子 Admin 登入 Email（儲存前 `Trim().ToLower()`）|
| `DefaultPassword` | `Admin@12345` | 預設密碼，**首登後強制變更** |
| `DisplayName` | `管理員` | 顯示名稱 |

> ⚠️ `DefaultPassword` 為敏感資訊，正式環境請以 User Secrets / 環境變數覆蓋，切勿沿用預設值。

---

## 4. 資料庫設計

### Entity 基底類別（`Models/Base/`）

| 類別 | 欄位 | 用途 |
|------|------|------|
| `BaseEntity` | Id（Guid）、CreatedAt | 唯讀紀錄（快照、訊息）|
| `AuditableEntity` | + UpdatedAt | 可修改資源（使用者）|
| `SoftDeletableEntity` | + IsDeleted、DeletedAt | 可軟刪除資源（角色、對話、Bot）|

- `Id` 由應用層產生（`Guid.NewGuid()`），非資料庫自增
- `CreatedAt` / `UpdatedAt` 由 `SaveChangesAsync` 覆寫自動填入（UTC）

### Fluent API 重要設定（`Data/AppDbContext.cs`）

**軟刪除全域篩選**：
```csharp
HasQueryFilter(p => !p.IsDeleted)  // 查詢自動排除已刪除資料
```
套用對象：Persona、PromptTemplate、BotBinding、Conversation

**索引設計**：
- `Users.Email`：唯一索引
- `PersonaVersions (PersonaId, Version)`：複合唯一
- `Conversations.ShareSlug`：過濾唯一索引（IS NOT NULL）
- `ExternalMessages (Platform, CreatedAt)`：複合索引（加速查詢）
- `TokenUsageStats (UserId, Date, ModelType, Source)`：複合唯一（UPSERT 用）
- `AiKeys (UserId, ProviderId)`：複合唯一（每人每供應商一把 Key）

**外鍵行為**：
- `SetNull`：刪除 User / Persona 時，相關關聯設 NULL（保留資料）
- `Cascade`：刪除 Conversation 時，級聯刪除所有 Message

### 資料表一覽

| 資料表 | 基底 | 說明 |
|--------|------|------|
| Users | AuditableEntity | 使用者帳號，BCrypt 密碼 |
| Personas | SoftDeletableEntity | AI 角色，支援版本快照 |
| PersonaVersions | BaseEntity | 唯讀版本快照，(PersonaId, Version) 唯一 |
| Conversations | SoftDeletableEntity | 對話，支援置頂與公開分享 |
| Messages | BaseEntity | 訊息，Cascade 刪除 |
| BotBindings | SoftDeletableEntity | LINE / Discord Bot 綁定，Token AES 加密 |
| PromptTemplates | SoftDeletableEntity | Prompt 模板庫 |
| ExternalMessages | BaseEntity | LINE / Discord 外部訊息監控紀錄 |
| TokenUsageStats | BaseEntity | Token 用量每日彙總統計 |
| AiKeys | AuditableEntity | AI 供應商金鑰，混合 BYOK（系統預設 + 使用者自帶），AES 加密 |

---

## 5. 統一回應格式與錯誤處理

### 成功回應（`DTOs/Common/ApiResponse.cs`）

```csharp
public class ApiResponse<T> {
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }

    public static ApiResponse<T> Ok(T data) => ...
    public static ApiResponse<T> Ok(T data, string message) => ...
    public static ApiResponse<T> Fail(string message) => ...
}
```

### 分頁回應（`DTOs/Common/PagedResponse.cs`）

```csharp
public class PagedResponse<T> {
    public List<T> Items { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
```

### 例外類別體系（`Common/Exceptions/`）

```
AppException（基底，帶 statusCode / errorCode / details）
├── ValidationException   → 422 VALIDATION_FAILED（附帶欄位錯誤字典）
├── NotFoundException     → 404 {RESOURCE}_NOT_FOUND
├── UnauthorizedException → 401 UNAUTHORIZED
├── ConflictException     → 409 CONFLICT
└── ForbiddenException    → 403 FORBIDDEN
```

### 全域例外中介層（`Middleware/ExceptionMiddleware.cs`）

攔截所有未處理例外，回傳 **RFC 7807 ProblemDetails** 格式：

```json
{
  "status": 404,
  "title": "Conversation 不存在",
  "detail": "Conversation '...' 不存在",
  "instance": "/api/v1/conversations/123",
  "errorCode": "CONVERSATION_NOT_FOUND",
  "traceId": "0HN1GDFRHJK01:00000001"
}
```

**例外對應規則**：

| 例外 | Status | ErrorCode | LogLevel |
|------|--------|-----------|----------|
| AppException 子類 | 依子類 | 機器可讀代碼 | Warning |
| DbUpdateConcurrencyException | 409 | CONCURRENCY_CONFLICT | Warning |
| DbUpdateException (UNIQUE) | 409 | DUPLICATE_ENTRY | Warning |
| DbUpdateException (FK) | 400 | FOREIGN_KEY_VIOLATION | Warning |
| OperationCanceledException | 499 | REQUEST_CANCELLED | Information |
| HttpRequestException | 502 | UPSTREAM_ERROR | Error |
| TaskCanceledException | 504 | UPSTREAM_TIMEOUT | Error |
| JsonException | 400 | INVALID_JSON | Information |
| 其他 | 500 | INTERNAL_ERROR | Error |

**前端 Axios 攔截器分工**（對應 `src/lib/api.ts`）：
- 元件層處理：400 / 404 / 409 / 422（行內錯誤）
- 攔截器全域 Toast：401 / 403 / 429 / 5xx / 網路錯誤

---

## 6. AI 多提供商整合

### 服務介面（`Services/AI/IAiService.cs`）

```csharp
public interface IAiService {
    // 後台 SSE 串流對話（驗證歸屬、寫入 DB）
    IAsyncEnumerable<ChatStreamChunk> ChatStreamAsync(
        ChatRequest request, Guid userId, CancellationToken cancellationToken);

    // 前台公開 SSE 串流（免登入、不寫入 DB，歷史由前端傳入）
    IAsyncEnumerable<ChatStreamChunk> PublicChatStreamAsync(
        PublicChatRequest request, CancellationToken cancellationToken);

    // 非串流單次呼叫（Prompt 強化用）；ownerUserId 決定金鑰歸屬，null = 系統預設
    Task<string> CompleteAsync(
        string systemPrompt, string userPrompt, Guid? ownerUserId = null,
        CancellationToken cancellationToken = default, bool allowSystemFallback = true);

    // 非串流 + 回傳 Token 用量（供 Webhook 等外部觸發寫入統計）
    // modelType 傳入則用該模型（如 LINE Bot 設定的模型）；null = 系統預設模型
    Task<(string Reply, int TokensIn, int TokensOut)> CompleteWithUsageAsync(
        string systemPrompt, string userPrompt, Guid? ownerUserId = null,
        CancellationToken cancellationToken = default, bool allowSystemFallback = true,
        string? modelType = null);
}
```

### 已支援提供商（`appsettings.json`）

| 提供商 | 代號 | 主要模型 | SupportsStreamUsage |
|--------|------|---------|-------------------|
| Groq | `groq` | Llama 3.3 70B、Llama 3.1 8B、Qwen3 32B | ✓ |
| Mistral | `mistral` | Mistral Small/Large、Open-Mistral-7B | ✗ |
| Cerebras | `cerebras` | GPT OSS 120B、ZAI GLM 4.7 | ✗ |
| SambaNova | `sambanova` | Llama 3.3 70B、DeepSeek V3.1 | ✗ |

模型格式：`{providerId}:{modelId}`，例如 `groq:llama-3.3-70b-versatile`

### SSE 串流實作重點（`Services/AI/OpenAiService.cs`）

- 使用 `IAsyncEnumerable<ChatStreamChunk>` 異步迭代器
- 支援 `CancellationToken`（前端斷線即停止，避免浪費 API 費用）
- 邊串流邊累積完整回應，完成後寫入 DB
- `IHttpClientFactory` 命名 Client（`ai:{providerId}`），避免 Socket 耗盡
- `SupportsStreamUsage` 旗標：部分提供商支援 `stream_options.include_usage`

### AI 錯誤處理共通化（`Common/Ai/AiError.cs`）

所有「狀態碼／例外 → 友善訊息」對應**集中於 `AiErrorClassifier`（單一事實來源）**，串流與非串流、四個載體共用：

- `AiErrorClassifier.Classify(statusCode, providerDisplay, errorCode?)` → `AiError`（含 `Kind`/`FriendlyMessage`/`Retryable`，衍生 `ChunkType`：限流→`rate_limit`，其餘→`error`）。
- **串流**（`CallOpenAiStreamAsync`，前台公開聊天 + 後台聊天室）：依 `ChunkType` 回 SSE chunk。
- **非串流**（`PostChatCompletionWithRetryAsync`）：丟 `UpstreamAiException(aiError)`，攜帶 `Kind`/`ChunkType`。
- **LINE / Discord**：`catch (UpstreamAiException)` → 回 `ex.Message`。
- 訊息中的供應商名稱依**當次實際呼叫的模型**解析，故各供應商（Groq/Mistral/Cerebras/SambaNova）顯示各自名稱。
- 詳細狀態對應表見 `skills/AI串接錯誤處理指南.md`。

> 修改錯誤訊息或新增分類只改 `AiErrorClassifier`，勿在各處重寫 switch。

### Prompt 強化功能（`Services/Personas/PersonaService.cs`）

```csharp
public async Task<EnhancePromptResponse> EnhancePromptAsync(EnhancePromptRequest request)
```

- 呼叫 `IAiService.CompleteAsync`（非串流）
- 有 `Instruction` 時以指定方向強化，無則通用優化
- 回傳強化結果供使用者預覽，**不自動存入 DB**
- 5xx 錯誤由全域攔截器處理，避免前端雙重 Toast

### AiController SSE 端點（`Controllers/Ai/AiController.cs`）

```csharp
[HttpPost("chat")]
// Response.ContentType = "text/event-stream; charset=utf-8"
// 逐行回傳 JSON chunk：{ type, content, usage }
// chunk type: delta / done / error / rate_limit

[HttpGet("providers")]
// 回傳 { id, displayName, isConfigured, models[] }
// isConfigured = ProviderKeys 中有對應 Key
```

### 混合 BYOK 金鑰解析（`Services/AI/AiKeyService.cs`）

AI 金鑰來源採「**系統預設 + 使用者自帶**」混合制，統一存於 `AiKeys` 表：

- **系統預設 Key**：`IsSystem = true`、掛在系統用戶（`Common/Constants/SystemUser`）下，由 API 啟動時的 `SeedSystemKeysAsync` 從 `appsettings` 加密寫入（冪等）。
- **使用者自帶 Key**：覆蓋系統預設，隔離個人額度。
- 金鑰均以 **AES-256-CBC 加密**（複用 `AesEncryptionHelper`），前端僅顯示後 4 碼。

```csharp
// 解析順序：使用者自帶（啟用中）→ 系統預設 → appsettings fallback
Task<string?> ResolveKeyAsync(
    Guid? ownerUserId, string providerId,
    bool allowSystemFallback = true, CancellationToken ct = default);
// allowSystemFallback = false（後台來源強制）：沒有自帶金鑰就回 null，不借用系統 Key
```

**AiKeysController（`Controllers/Admin/AiKeys/`，路由 `/api/v1/users/me/ai-keys`，需 JWT）**：

| Method | 端點 | 說明 |
|--------|------|------|
| GET | `/api/v1/users/me/ai-keys` | 取得自己的金鑰清單（僅後 4 碼，不含系統預設）|
| PUT | `/api/v1/users/me/ai-keys` | 新增 / 更新某供應商金鑰（儲存前驗證有效性）|
| DELETE | `/api/v1/users/me/ai-keys/{providerId}` | 刪除某供應商金鑰 |

### Webhooks（外部平台整合）

LINE / Discord Bot 透過 Webhook 接收外部訊息，端點**不掛 `/api/v1` 前綴、免 JWT**，改以平台簽章驗證：

| Method | 端點 | 驗簽方式 | 處理 |
|--------|------|---------|------|
| POST | `/webhooks/line/{botId:guid}` | LINE Signature：`HMAC-SHA256(rawBody, ChannelSecret)` → Base64 比對 `X-Line-Signature` | `LineWebhookService` + `LineCredentialService` |
| POST | `/webhooks/discord/{botId:guid}` | Ed25519（`NSec.Cryptography`）：驗證訊息 = `UTF8(timestamp + rawBody)`，header `X-Signature-Ed25519` / `-Timestamp` | `DiscordWebhookService` + `DiscordCommandService` |

- 驗簽在 Controller 層用**原始 body bytes** 進行（不可讓框架先解析）；偽造請求回 401。
- 機敏憑證 `BotToken` / `ChannelSecret` 在 `BotBindings` 以 AES 加密，由 Service 內部解密使用；而 `DiscordPublicKey` 為**公鑰、明文儲存**，由 Controller 直接讀取驗簽（未設定時僅記 Warning 並放行，限本機開發）。
- Discord 以 `DiscordCommandService` 呼叫 Discord API 自動註冊 Global `/chat` Slash Command；註冊結果寫入 `BotBindings.CredentialValid`（LINE 則以 `GET /v2/bot/info` 驗證 Channel Access Token），供後台標示無效 Bot。
- 收到訊息 → 查 `BotBinding` + `Persona` → 以 **該 Bot 設定的模型**呼叫 AI（LINE 走 `CompleteWithUsageAsync(modelType: ...)`、Discord 走 `CompleteWithToolsAsync(modelType)`，皆以 Bot 擁有者解析金鑰）→ 寫入 `ExternalMessages` 與 `TokenUsageStats`。
- AI 上游錯誤（429/401/逾時等）：兩者皆 `catch (UpstreamAiException)` → 直接回覆 `ex.Message`（友善訊息，含對應供應商名稱），與聊天室一致（詳見下方「AI 錯誤處理共通化」）。
- **Discord 信任系統（V012）**：讓角色依發話者身分切換語氣。成員名單存 `BotBinding.TrustedUsersJson`（JSON，每筆含 `systemRole`＝`owner`/`trusted`，可多位 owner），讀寫單一來源 `BotTrustService`。維護路徑兩條——①後台 CRUD（`/bot-bindings/{id}/trusted-users`，App 登入授權，可設系統角色）②Discord 對話由現任主人要求、加入前跳二次確認（對話路徑一律加 `trusted`，不開放提權）。主人閘門在 **code 層**（`IsOwnerAsync`），非靠 prompt。`DiscordWebhookService.BuildTrustContextAsync` 把「身分事實」注入 system prompt、語氣交由角色設定本身（中性化，可套任意角色）。詳見 `discord-bot-function-calling-design.md` §12。

---

## 7. 資料加密

### AES-256-CBC（`Common/Helpers/AesEncryptionHelper.cs`）

```csharp
public class AesEncryptionHelper {
    public string Encrypt(string plainText)   // 加密
    public string Decrypt(string cipherText)  // 解密
}
```

**設定**（`Common/Settings/EncryptionSettings.cs`）：
- `Key`：32 bytes Base64（AES-256）
- `Iv`：16 bytes Base64

**應用場景**：
- `BotBinding.BotTokenEncrypted`：LINE / Discord Bot Token
- `BotBinding.ChannelSecretEncrypted`：LINE Channel Secret
- `AiKey.EncryptedKey`：AI 供應商 API 金鑰（系統預設 + 使用者自帶）
- 前端顯示只回傳後 4 碼（`TokenLastFour` / `KeyLastFour`），不需解密

**注入**：`Singleton`，Key/IV 只載入一次

---

## 8. 物件分層設計規範

### 三層物件職責

| 物件類型 | 位置 | 職責 | 說明 |
|---------|------|------|------|
| Entity | `Models/` | SQL 完整對應 | 含 PasswordHash、BotTokenEncrypted 等敏感欄位 |
| Domain | `Domain/` | 業務物件（Service 回傳） | 已過濾敏感欄位，可含 JOIN 計算欄位（PersonaName 等）|
| Request DTO | `DTOs/Requests/` | 驗證前端輸入 | 含 `[Required]`、`[MaxLength]` 等驗證 Attribute |
| Response DTO | `DTOs/Responses/` | 定義 API 合約 | 列表與詳情分開定義，欄位按情境選取 |

**Monitor 和 AI 服務例外**：產出聚合統計資料或 SSE 協議物件，Service 直接回傳 DTO（不走 Domain 層）。

### DTO 命名規則

| 用途 | 命名 |
|------|------|
| 列表摘要 | `{Resource}ListItem` |
| 單筆詳情 | `{Resource}DetailResponse` |
| 建立請求 | `Create{Resource}Request` |
| 更新請求 | `Update{Resource}Request` |
| 查詢條件 | `{Resource}QueryParams` |
| 版本相關 | `{Resource}VersionItem` / `{Resource}VersionDetailResponse` |

### Entity XML 文件規範

所有 Entity 欄位都需要 XML `<summary>`（用途說明）和 `<remarks>`（SQL 型別 + 約束）：

```csharp
/// <summary>角色名稱，用於列表顯示與搜尋</summary>
/// <remarks>nvarchar(100) · NOT NULL</remarks>
public string Name { get; set; } = string.Empty;

/// <summary>使用的 AI 模型識別碼，例如 "groq:llama-3.3-70b-versatile"</summary>
/// <remarks>nvarchar(50) · NOT NULL · FK → Users ON DELETE SET NULL</remarks>
public Guid? UserId { get; set; }
```

`<remarks>` 格式：`{SQL型別} · {NOT NULL | NULL} · {其他限制}`

### 常數定義（`Common/Constants/`）

```csharp
MessageRole    // user / assistant / system
Platform       // line / discord / web
TokenSource    // admin / web / line / discord
SystemUser     // 系統用戶 Id（系統預設 AI Key 的擁有者）
```

---

## 9. 日誌與監控

### Serilog 設定

```csharp
.WriteTo.Console()
.WriteTo.File("logs/log-.txt",
    rollingInterval: RollingInterval.Day,
    retainedFileCountLimit: 30)
```

請求日誌格式：`HTTP {Method} {Path} => {StatusCode} ({Elapsed} ms)`

ExceptionMiddleware 分級記錄：預期例外 Warning，未知例外 Error

### 健康檢查

```
GET /health  →  檢查 API 服務與資料庫連線狀態
```

---

## 10. CORS 設定

允許來源（開發環境）：
- `http://localhost:5173`（Vite 後台）
- `http://localhost:3000`（Next.js 前台）

中介層順序：`UseCors` 必須在 `UseAuthentication` 之前

---

## 11. 敏感資訊管理

| 層級 | 內容 |
|------|------|
| `appsettings.json` | 非敏感設定（CORS、AI 提供商清單、AppName）|
| `appsettings.Development.json` | 開發用 API Keys（已加入 .gitignore）|
| User Secrets | JwtSettings:SecretKey、AI ProviderKeys、EncryptionSettings |
| 環境變數 | 生產部署（IIS 應用程式池 / Docker ENV）|

---

## 維護說明

每次新增以下內容時，請同步更新此文件：
- 新增 NuGet 套件
- 新增 API 端點或 Service 方法
- 新增 Entity 或修改資料庫結構
- 新增 AI 提供商
- 修改認證 / 加密 / 錯誤處理邏輯
