# ReactL API

ASP.NET Core 8 後端 API，提供 AI Prompt 管理、多輪對話、Persona 角色設計，以及 LINE / Discord Bot 整合。

---

## 技術棧

| 層級 | 技術 |
|------|------|
| 語言 / 執行環境 | C# 12 / .NET 8 |
| Web 框架 | ASP.NET Core 8 (Controllers) |
| ORM | Entity Framework Core 8（執行期 ORM，Code First 模型）|
| Schema 管理 | **SqlScripts 版本化 SQL 腳本**（不使用 EF Migration）|
| 資料庫 | SQL Server (開發: LocalDB / SQLEXPRESS) |
| 認證 | JWT Bearer (HMAC-SHA256) |
| 密碼雜湊 | BCrypt.Net-Next 4 |
| 資料加密 | AES-256-CBC |
| 日誌 | Serilog (Console + 每日滾動檔案) |
| API 文件 | Swagger / OpenAPI 3 |
| AI 整合 | OpenAI 相容 API (多 Provider + SSE 串流) |
| 健康檢查 | EF Core HealthCheck (`GET /health`) |

---

## 功能模組

| 路由前綴 | 功能說明 |
|----------|----------|
| `POST /api/v1/auth` | 使用者註冊 / 登入，JWT 發行 |
| `GET/PUT /api/v1/users` | 使用者管理、啟用 / 停用 |
| `CRUD /api/v1/personas` | AI Persona 建立、版本管理、系統提示詞編輯 |
| `CRUD /api/v1/conversations` | 多輪對話管理、釘選、公開分享 |
| `POST /api/v1/ai/chat` | SSE 串流 AI 回應、多 Provider 選擇 |
| `CRUD /api/v1/prompttemplates` | Prompt 範本庫，支援分類與標籤 |
| `CRUD /api/v1/botbindings` | LINE / Discord Bot 設定，Token AES 加密儲存 |
| `GET /api/v1/monitor` | 監控外部 Bot 對話、Token 用量統計（messages / conversations / stats/tokens）|
| `GET/PUT/DELETE /api/v1/users/me/ai-keys` | 使用者 AI 金鑰管理（混合 BYOK：自帶 Key 覆蓋系統預設）|
| `POST /webhooks/{line\|discord}/{botId}` | 外部平台 Webhook（免 JWT，平台簽章驗證；LINE HMAC-SHA256、Discord Ed25519）|

---

## AI Provider 設定

支援以下 OpenAI 相容 Provider（API Key 透過 User Secrets 設定）：

- **Groq** — `llama-3.3-70b-versatile`、`llama-3.1-8b-instant`、`qwen3-32b`
- **Mistral** — Small、Large、7B
- **Cerebras** — GPT OSS 120B、ZAI GLM 4.7
- **SambaNova** — Llama 3.3 70B、Llama 4 Maverick、DeepSeek V3.1

預設模型：`groq:llama-3.3-70b-versatile`

---

## Clone 後需補齊的設定

`.gitignore` 排除了以下**不進版控**的檔案與資料夾，Clone 後需手動補齊：

| 排除項目 | 說明 | 如何補齊 |
|----------|------|----------|
| `ReactL.api/appsettings.Development.json` | 本機開發所有敏感設定 | 從備份複製，或參考下方內容手動建立 |
| `ReactL.api/appsettings.Production.json` | 正式環境設定 | 依部署環境建立或使用環境變數 |
| `bin/` / `obj/` | 建置產出 | `dotnet build` 自動產生 |
| `logs/` / `*.log` | Serilog 日誌檔 | 執行時自動產生 |
| `.vs/` | Visual Studio 工作區設定 | IDE 自動產生 |

### appsettings.Development.json 完整結構

此檔案集中管理**所有本機開發的敏感設定**，不進版控。建立後請填入實際值：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=你的機器名稱\\SQLEXPRESS;Database=ReactL;User Id=sa;Password=你的密碼;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "JwtSettings": {
    "SecretKey": "填入 32 bytes Base64 隨機字串"
  },
  "EncryptionSettings": {
    "Key": "填入 32 bytes Base64（AES-256 金鑰）",
    "Iv":  "填入 16 bytes Base64（AES CBC 初始向量）"
  },
  "AiSettings": {
    "DefaultModel": "groq:llama-3.3-70b-versatile",
    "MaxTokens": 8192,
    "ProviderKeys": {
      "groq":       "gsk_...",
      "mistral":    "...",
      "cerebras":   "csk-...",
      "sambanova":  "..."
    }
  }
}
```

產生 JwtSettings:SecretKey 和 EncryptionSettings 的值（PowerShell）：

```powershell
$rng = [System.Security.Cryptography.RNGCryptoServiceProvider]::new()

# JwtSettings:SecretKey 和 EncryptionSettings:Key（32 bytes）
$buf = New-Object byte[] 32; $rng.GetBytes($buf)
[Convert]::ToBase64String($buf)

# EncryptionSettings:Iv（16 bytes）
$buf = New-Object byte[] 16; $rng.GetBytes($buf)
[Convert]::ToBase64String($buf)
```

> **⚠️ AES Key / IV 一旦設定且資料庫有 Bot 資料後不可更換**，否則已加密儲存的 Bot Token 將無法解密。請務必另行備份這兩個值。

---

## 換機器遷移步驟

### 前置軟體

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- SQL Server（或 SQL Server Express）
- SQL 用戶端工具（SSMS / Azure Data Studio / `sqlcmd`），用於執行 SqlScripts

### 步驟

```
1. git clone https://github.com/ZE777/ReactL.api.git
2. 將備份的 appsettings.Development.json 複製到 ReactL.api/ReactL.api/ReactL.api/
3. 修改 DefaultConnection 的 Server 名稱為新機器的 SQL Server 執行個體
4. cd ReactL.api/ReactL.api/ReactL.api
5. dotnet restore
6. 依序執行 SqlScripts 建立資料庫（見下方「建立資料庫」）
7. dotnet run
```

### 各設定的遷移方式

| 設定項目 | 換機器要做什麼 |
|----------|---------------|
| `ConnectionStrings:DefaultConnection` | **必須修改** Server 名稱（每台機器不同） |
| `JwtSettings:SecretKey` | 直接從備份複製，值相同（Token 才能跨部署解析） |
| `EncryptionSettings:Key / Iv` | **必須完全相同**，否則舊 Bot Token 無法解密 |
| AI Provider Keys（Groq/Mistral 等） | 直接從備份複製，Key 本身跨機器通用 |
| `appsettings.json` | 不需處理，git pull 自動取得 |

---

## 快速開始（首次設定）

### 1. 還原相依套件

```bash
dotnet restore
```

### 2. 建立並填寫 appsettings.Development.json

參考上方「appsettings.Development.json 完整結構」，建立檔案並填入所有值。

### 3. 建立資料庫

Schema 以 `SqlScripts/` 下的版本化腳本管理（已移除 EF Migration）。在目標資料庫**依序**執行：

```
SqlScripts/Init/V001_CreateSchema.sql        ← 初始全結構
SqlScripts/Migrations/V002 … V007            ← 依編號順序執行的增量變更
SqlScripts/Seed/Seed_OfficialPersonas.sql    ← 官方內建 Persona 種子
```

各腳本皆以 `IF NOT EXISTS` 等條件包裹，具**冪等性**，重複執行不會報錯。  
系統預設 AI Key 不在腳本中，改由 API 首次啟動時的 seeder 從 `appsettings` 加密寫入。

> 用 `sqlcmd` 範例：`sqlcmd -S 你的Server -d ReactL -i SqlScripts\Init\V001_CreateSchema.sql`

### 4. 啟動

```bash
dotnet run
```

Swagger UI：`https://localhost:{port}/swagger`
健康檢查：`https://localhost:{port}/health`

> **首次啟動會自動建立預設 Admin**：當資料庫尚無任何 `Role=Admin` 帳號時，啟動種子（`AdminSeeder`）會依 `AdminSeedSettings` 建立一支 Admin（預設 `admin@reactl.local` / `Admin@12345`），並以系統預設 AI Key 自動綁定其金鑰，首次登入後會**強制改密**。正式環境請以 User Secrets / 環境變數覆蓋預設密碼；若不需要可將 `AdminSeedSettings:Enabled` 設為 `false`。詳見 `workflows/backend-tech-overview.md` 第 3 節。

---

## 專案結構

```
ReactL.api/
├── Controllers/
│   ├── Admin/          # 後台登入使用者 API（Auth/Personas/Conversations/AiKeys 等）
│   ├── Web/            # 公開前台 API（SharedConversations/PublicChat 等）
│   └── Webhooks/       # LINE / Discord 平台回呼（免 JWT，簽章驗證）
├── Services/           # 業務邏輯層，直接注入 AppDbContext（含 AI / Webhooks）
├── Domain/             # 業務物件層，Service 回傳型別（不含敏感欄位）
├── Models/             # EF Core Entity，唯一對應 SQL 資料表的位置
├── DTOs/
│   ├── Common/         # ApiResponse<T>、PagedResponse<T>
│   ├── Requests/       # 前端輸入物件，含 DataAnnotations 驗證
│   └── Responses/      # API 回傳合約，列表與詳情分開定義
├── Data/               # AppDbContext（Entity 配置集中於 OnModelCreating）
├── Common/
│   ├── Settings/       # 強型別設定類別
│   ├── Exceptions/     # AppException 層級體系
│   ├── Helpers/        # AES 加密工具
│   ├── Constants/      # MessageRole / Platform / TokenSource / SystemUser
│   └── Extensions/     # ClaimsPrincipal 擴充
├── Middleware/         # 全域例外處理
└── SqlScripts/         # 版本化 schema 腳本（Init / Migrations / Seed），取代 EF Migration
```

**資料流向**：`Request DTO → Controller → Service（Entity → Domain）→ Controller（Domain → Response DTO）→ 前端`

---

## 安全性說明

- 所有敏感設定（JWT Secret、API Key、AES Key）集中於 **appsettings.Development.json**（已加入 .gitignore）或正式環境的環境變數，不進版控
- `appsettings.Development.json` / `appsettings.Production.json` 已加入 `.gitignore`
- Bot Token 與 Channel Secret 以 AES-256-CBC 加密後才儲存至資料庫
- 登入錯誤回傳統一訊息，防止帳號列舉攻擊

### Bot 憑證安全設計（AES 加密 + 不回傳原值）

Bot Token 與 Channel Secret 採取「**只進不出**」原則：

| 操作 | 行為 |
|------|------|
| 建立 / Rotate | 明文 → `AesEncryptionHelper.Encrypt()` → 存入 DB，同時儲存後 4 碼（`TokenLastFour`）|
| 讀取（列表/詳情）| 只回傳 `TokenLastFour`，加密欄位不出現在任何 DTO |
| Webhook 驗簽 | Service 層內部 `Decrypt()` 使用，結果不離開後端 |
| 前端編輯表單 | 欄位留空 = 保持原值不動；填入新值 = 觸發 Rotate |

**為什麼 AES 可解密但 API 不回傳**：

AES 加密解決的是「**靜態儲存**」風險（資料庫洩漏時攻擊者拿到的是密文）。  
若 API 將解密後的明文回傳給前端，等同於加密形同虛設，因為：

- HTTPS 雖加密傳輸，但 Browser DevTools Network 面板直接可見 Response Body
- 若前端存在 XSS 漏洞，JS 記憶體中的 Token 可被竊取
- 前端錯誤追蹤工具（如 Sentry）可能把 API Response 記錄進日誌

**結論**：憑證一旦儲存，只在後端內部使用，前端僅能看到後 4 碼作為識別用途。忘記 Token 的正確做法是回到 LINE / Discord Developer Console 重新取得，再透過 Rotate Token 更新。

---

## 錯誤處理

全域 Middleware 依照 RFC 7807 格式回傳 `ProblemDetails`：

| Exception 類型 | HTTP 狀態碼 |
|----------------|-------------|
| `ValidationException` | 422 |
| `NotFoundException` | 404 |
| `UnauthorizedException` | 401 |
| `ForbiddenException` | 403 |
| `ConflictException` | 409 |
| `UpstreamAiException` | 503 |
| 請求取消 | 499 |
| 上游逾時 | 504 |
