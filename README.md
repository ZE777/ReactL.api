# ReactL API

ASP.NET Core 8 後端 API，提供 AI Prompt 管理、多輪對話、Persona 角色設計，以及 LINE / Discord Bot 整合。

---

## 技術棧

| 層級 | 技術 |
|------|------|
| 語言 / 執行環境 | C# 12 / .NET 8 |
| Web 框架 | ASP.NET Core 8 (Controllers) |
| ORM | Entity Framework Core 8 (Code First) |
| 資料庫 | SQL Server (開發: LocalDB) |
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
| `GET /api/v1/monitor` | 監控外部 Bot 對話、Token 用量統計 |

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

`.gitignore` 排除了以下**不進版控**的檔案與資料夾，Clone 後需手動建立或設定：

| 排除項目 | 說明 | 如何補齊 |
|----------|------|----------|
| `ReactL.api/appsettings.Development.json` | 本機開發設定（含 DB 連線字串） | 參考下方範例自行建立 |
| `ReactL.api/appsettings.Production.json` | 正式環境設定 | 依部署環境建立或使用環境變數 |
| `bin/` / `obj/` | 建置產出 | `dotnet build` 自動產生 |
| `logs/` / `*.log` | Serilog 日誌檔 | 執行時自動產生 |
| `.vs/` | Visual Studio 工作區設定 | IDE 自動產生 |

### appsettings.Development.json 範例

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=ReactLDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

### User Secrets（不存於專案目錄，需個別設定）

以下機敏值透過 .NET User Secrets 管理，**不會出現在任何設定檔**，需在每台開發機器上執行：

```bash
cd ReactL.api

# JWT 簽名金鑰（建議 32 字元以上隨機字串）
dotnet user-secrets set "Jwt:SecretKey" "<your-jwt-secret>"

# AES 加密金鑰（Bot Token 加密用）
dotnet user-secrets set "Encryption:Key" "<32-byte-hex-key>"
dotnet user-secrets set "Encryption:IV"  "<16-byte-hex-iv>"

# AI Provider API Keys（依需求設定）
dotnet user-secrets set "AiSettings:Providers:0:ApiKey" "<groq-api-key>"
dotnet user-secrets set "AiSettings:Providers:1:ApiKey" "<mistral-api-key>"
dotnet user-secrets set "AiSettings:Providers:2:ApiKey" "<cerebras-api-key>"
dotnet user-secrets set "AiSettings:Providers:3:ApiKey" "<sambanova-api-key>"
```

> User Secrets 儲存於 `%APPDATA%\Microsoft\UserSecrets\<project-id>\secrets.json`，不在專案目錄內。

---

## 快速開始

### 前置需求

- .NET 8 SDK
- SQL Server LocalDB（或其他 SQL Server 執行個體）

### 1. 還原相依套件

```bash
dotnet restore
```

### 2. 建立 appsettings.Development.json 並設定 User Secrets

參考上方「Clone 後需補齊的設定」章節。

### 3. 建立資料庫

```bash
dotnet ef database update
```

### 4. 啟動

```bash
dotnet run
```

Swagger UI：`https://localhost:{port}/swagger`
健康檢查：`https://localhost:{port}/health`

---

## 專案結構

```
ReactL.api/
├── Controllers/        # API 端點
├── Services/           # 業務邏輯層
├── Models/             # EF Core 實體
├── DTOs/               # 請求 / 回應資料結構
├── Data/               # DbContext 與工廠
├── Common/
│   ├── Settings/       # 強型別設定類別
│   ├── Exceptions/     # AppException 層級體系
│   ├── Helpers/        # AES 加密工具
│   └── Extensions/     # ClaimsPrincipal 擴充
├── Middleware/         # 全域例外處理
├── Migrations/         # EF Core 遷移記錄
└── SqlScripts/         # 初始化 SQL 腳本
```

---

## 安全性說明

- 所有敏感設定（JWT Secret、API Key、AES Key）使用 **User Secrets** 或環境變數，不進版控
- `appsettings.Development.json` / `appsettings.Production.json` 已加入 `.gitignore`
- Bot Token 與 Channel Secret 以 AES-256-CBC 加密後才儲存至資料庫
- 登入錯誤回傳統一訊息，防止帳號列舉攻擊

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
| 請求取消 | 499 |
| 上游逾時 | 504 |
