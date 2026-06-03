# 資料庫結構與資料流動文件

> 最後更新：2026-06-02
> 資料庫：SQL Server（開發 LocalDB / 生產 SQL Server）
> ORM：Entity Framework Core 8（Code First + Fluent API）

---

## 目錄

1. [Entity 基底類別](#1-entity-基底類別)
2. [資料表一覽](#2-資料表一覽)
3. [完整欄位定義](#3-完整欄位定義)
4. [關聯與外鍵行為](#4-關聯與外鍵行為)
5. [索引策略](#5-索引策略)
6. [軟刪除設計](#6-軟刪除設計)
7. [ER 關聯圖](#7-er-關聯圖)
8. [資料流動流程](#8-資料流動流程)

---

## 1. Entity 基底類別

所有資料表都繼承自以下三層基底，由上往下累加欄位：

```
BaseEntity
│  Id: Guid（應用層產生，非資料庫自增）
│  CreatedAt: DateTime（UTC，SaveChangesAsync 自動填入，新增後不覆寫）
│
├─ AuditableEntity
│    UpdatedAt: DateTime（UTC，新增與修改時都自動更新）
│
└─ SoftDeletableEntity
     IsDeleted: bool（預設 false）
     DeletedAt: DateTime?（軟刪除時由 Service 層填入）
```

**Global Query Filter**：所有繼承 `SoftDeletableEntity` 的資料表，查詢時自動加上 `WHERE IsDeleted = 0`，不需手動過濾。

---

## 2. 資料表一覽

| 資料表 | 基底類別 | 主要用途 |
|--------|---------|---------|
| Users | AuditableEntity | 使用者帳號，BCrypt 密碼，角色權限 |
| Personas | SoftDeletableEntity | AI 角色定義，System Prompt，版本管理 |
| PersonaVersions | BaseEntity | Persona 每次儲存的唯讀版本快照 |
| Conversations | SoftDeletableEntity | 使用者對話紀錄，支援置頂與公開分享 |
| Messages | BaseEntity | 對話內的每一則訊息，含 Token 用量 |
| BotBindings | SoftDeletableEntity | LINE / Discord Bot 設定，Token AES 加密 |
| PromptTemplates | SoftDeletableEntity | 使用者建立的 Prompt 模板庫 |
| ExternalMessages | BaseEntity | LINE / Discord 外部 Bot 對話監控紀錄 |
| TokenUsageStats | BaseEntity | 每日 Token 用量彙總（UPSERT 模式）|

---

## 3. 完整欄位定義

### Users

| 欄位 | 型別 | 限制 | 說明 |
|------|------|------|------|
| Id | Guid | PK | 應用層產生 |
| CreatedAt | DateTime | NOT NULL | UTC |
| UpdatedAt | DateTime | NOT NULL | UTC，自動更新 |
| Email | nvarchar(256) | NOT NULL, UNIQUE | 不分大小寫查詢 |
| PasswordHash | nvarchar(500) | NOT NULL | BCrypt 雜湊 |
| DisplayName | nvarchar(100) | NOT NULL | UI 顯示名稱 |
| Role | nvarchar(20) | NOT NULL, DEFAULT 'User' | User / Admin |
| IsActive | bit | NOT NULL, DEFAULT 1 | false 時登入被拒 |
| LastLoginAt | DateTime | NULL | 安全稽核用 |

---

### Personas

| 欄位 | 型別 | 限制 | 說明 |
|------|------|------|------|
| Id | Guid | PK | |
| CreatedAt | DateTime | NOT NULL | |
| UpdatedAt | DateTime | NOT NULL | |
| IsDeleted | bit | NOT NULL, DEFAULT 0 | 軟刪除 |
| DeletedAt | DateTime | NULL | |
| UserId | Guid | FK → Users, NULL | NULL = 系統內建角色 |
| Name | nvarchar(100) | NOT NULL | 角色名稱 |
| Emoji | nvarchar(10) | NULL | 前台圖示，例如 🤖 |
| SystemPrompt | nvarchar(max) | NOT NULL | 當前完整 System Prompt |
| PromptSections | nvarchar(max) | NULL | Prompt Builder 各區塊 JSON |
| CurrentVersion | int | NOT NULL, DEFAULT 1 | 對應最新 PersonaVersion.Version |
| IsBuiltin | bit | NOT NULL, DEFAULT 0 | true = 系統內建，不可刪除 |

`PromptSections` JSON 結構：
```json
{
  "role": "你是一位...",
  "background": "服務於...",
  "task": "負責...",
  "format": "條列式...",
  "constraints": "不得...",
  "examples": "Q: ...\nA: ..."
}
```

---

### PersonaVersions

| 欄位 | 型別 | 限制 | 說明 |
|------|------|------|------|
| Id | Guid | PK | |
| CreatedAt | DateTime | NOT NULL | 快照建立時間 |
| PersonaId | Guid | FK → Personas CASCADE | |
| Version | int | NOT NULL | 同 Persona 內遞增 |
| SystemPrompt | nvarchar(max) | NOT NULL | 此版本的 Prompt 快照 |
| PromptSections | nvarchar(max) | NULL | 此版本的區塊快照 JSON |
| ChangeNote | nvarchar(500) | NULL | 修改說明 |

複合唯一：`(PersonaId, Version)`

---

### Conversations

| 欄位 | 型別 | 限制 | 說明 |
|------|------|------|------|
| Id | Guid | PK | |
| CreatedAt | DateTime | NOT NULL | |
| UpdatedAt | DateTime | NOT NULL | |
| IsDeleted | bit | NOT NULL, DEFAULT 0 | |
| DeletedAt | DateTime | NULL | |
| UserId | Guid | FK → Users | |
| PersonaId | Guid | FK → Personas, NULL, SET NULL | 刪除 Persona 時設 NULL |
| Title | nvarchar(200) | NOT NULL, DEFAULT '新對話' | |
| ModelType | nvarchar(50) | NOT NULL | 例如 groq:llama-3.3-70b-versatile |
| IsPinned | bit | NOT NULL, DEFAULT 0 | 置頂顯示 |
| IsPublic | bit | NOT NULL, DEFAULT 0 | 公開分享開關 |
| ShareSlug | nvarchar(20) | NULL, UNIQUE(FILTERED) | 公開分享短碼 |

過濾唯一索引：ShareSlug 唯一，但僅在 `[ShareSlug] IS NOT NULL` 時生效，允許多筆 NULL。

---

### Messages

| 欄位 | 型別 | 限制 | 說明 |
|------|------|------|------|
| Id | Guid | PK | |
| CreatedAt | DateTime | NOT NULL | |
| ConversationId | Guid | FK → Conversations CASCADE | |
| Role | nvarchar(20) | NOT NULL | user / assistant / system |
| Content | nvarchar(max) | NOT NULL | 訊息內容 |
| TokensIn | int | NOT NULL, DEFAULT 0 | user 訊息時記錄 |
| TokensOut | int | NOT NULL, DEFAULT 0 | assistant 訊息時記錄 |

不使用軟刪除，Conversation 刪除時 CASCADE 清除。

---

### BotBindings

| 欄位 | 型別 | 限制 | 說明 |
|------|------|------|------|
| Id | Guid | PK | |
| CreatedAt | DateTime | NOT NULL | |
| UpdatedAt | DateTime | NOT NULL | |
| IsDeleted | bit | NOT NULL, DEFAULT 0 | |
| DeletedAt | DateTime | NULL | |
| UserId | Guid | FK → Users | |
| PersonaId | Guid | FK → Personas, NULL, SET NULL | |
| Platform | nvarchar(20) | NOT NULL | line / discord |
| BotName | nvarchar(100) | NOT NULL | Bot 顯示名稱 |
| BotTokenEncrypted | nvarchar(700) | NOT NULL | AES-256-CBC 加密，Base64 |
| ChannelSecretEncrypted | nvarchar(700) | NULL | LINE 專用，AES-256-CBC |
| TokenLastFour | nvarchar(4) | NOT NULL | 後 4 碼，避免頻繁解密 |
| ModelType | nvarchar(50) | NOT NULL | Bot 使用的 AI 模型 |
| IsEnabled | bit | NOT NULL, DEFAULT 1 | false = 不回應 Webhook |

---

### PromptTemplates

| 欄位 | 型別 | 限制 | 說明 |
|------|------|------|------|
| Id | Guid | PK | |
| CreatedAt | DateTime | NOT NULL | |
| UpdatedAt | DateTime | NOT NULL | |
| IsDeleted | bit | NOT NULL, DEFAULT 0 | |
| DeletedAt | DateTime | NULL | |
| UserId | Guid | FK → Users | |
| Title | nvarchar(200) | NOT NULL | 模板標題 |
| Content | nvarchar(max) | NOT NULL | 模板內容 |
| Category | nvarchar(50) | NOT NULL, DEFAULT '其他' | 寫作 / 程式 / 翻譯 / 其他 |
| Tags | nvarchar(500) | NULL | 逗號分隔標籤，例如 "精簡,技術" |
| UsageCount | int | NOT NULL, DEFAULT 0 | 使用次數，熱門排序用 |

---

### ExternalMessages

| 欄位 | 型別 | 限制 | 說明 |
|------|------|------|------|
| Id | Guid | PK | |
| CreatedAt | DateTime | NOT NULL | |
| BotBindingId | Guid | FK → BotBindings CASCADE | |
| Platform | nvarchar(20) | NOT NULL | 冗餘欄位，加速 Monitor 頁篩選 |
| ExternalUserId | nvarchar(200) | NOT NULL | LINE userId / Discord userId |
| ExternalChannelId | nvarchar(200) | NULL | Discord 頻道 ID |
| Role | nvarchar(20) | NOT NULL | user / assistant |
| Content | nvarchar(max) | NOT NULL | 訊息內容 |
| TokensIn | int | NOT NULL, DEFAULT 0 | |
| TokensOut | int | NOT NULL, DEFAULT 0 | |

---

### TokenUsageStats

| 欄位 | 型別 | 限制 | 說明 |
|------|------|------|------|
| Id | Guid | PK | |
| CreatedAt | DateTime | NOT NULL | |
| UserId | Guid | FK → Users | |
| Date | DateOnly | NOT NULL | UTC 日期（無時間） |
| ModelType | nvarchar(50) | NOT NULL | AI 模型識別碼 |
| Source | nvarchar(20) | NOT NULL | admin / web / line / discord |
| TokensIn | int | NOT NULL, DEFAULT 0 | 當日累計輸入 Token |
| TokensOut | int | NOT NULL, DEFAULT 0 | 當日累計輸出 Token |
| RequestCount | int | NOT NULL, DEFAULT 0 | 當日請求次數 |

複合唯一：`(UserId, Date, ModelType, Source)`，UPSERT 時以此為查找鍵。

---

## 4. 關聯與外鍵行為

| 父資料表 | 子資料表 | 外鍵欄位 | 刪除行為 | 說明 |
|---------|---------|---------|---------|------|
| Users | Personas | UserId | SET NULL | 刪除使用者，保留 Persona（UserId → NULL）|
| Personas | PersonaVersions | PersonaId | CASCADE | 刪除 Persona，版本全部清除 |
| Users | PromptTemplates | UserId | CASCADE | 刪除使用者，模板一起刪 |
| Users | BotBindings | UserId | CASCADE | 刪除使用者，Bot 一起刪 |
| Personas | BotBindings | PersonaId | SET NULL | 刪除 Persona，Bot 保留（PersonaId → NULL）|
| Users | Conversations | UserId | CASCADE | 刪除使用者，對話一起刪 |
| Personas | Conversations | PersonaId | SET NULL | 刪除 Persona，對話保留（PersonaId → NULL）|
| Conversations | Messages | ConversationId | CASCADE | 刪除對話，訊息全部清除 |
| BotBindings | ExternalMessages | BotBindingId | CASCADE | 刪除 Bot，外部訊息紀錄清除 |
| Users | TokenUsageStats | UserId | CASCADE | 刪除使用者，統計一起刪 |

**設計原則**：
- 使用者生成的「創作內容」（Persona）刪除 User 時保留，以防誤刪
- 訊息、統計等「衍生資料」跟隨父資料一起清除
- Bot 綁定 Persona 是可選的，Persona 刪除後 Bot 仍可繼續運作（只是無角色）

---

## 5. 索引策略

| 索引名稱 | 資料表 | 欄位 | 類型 | 目的 |
|---------|--------|------|------|------|
| IX_Users_Email | Users | Email | 唯一 | 登入查詢、防重複帳號 |
| IX_Personas_UserId | Personas | UserId | 一般 | 列出使用者的 Persona |
| UX_PersonaVersions | PersonaVersions | (PersonaId, Version) | 複合唯一 | 版本號不重複 |
| IX_Conversations_UserId | Conversations | UserId | 一般 | 列出使用者的對話 |
| IX_Conversations_ShareSlug | Conversations | ShareSlug | 過濾唯一 | 公開分享查詢（IS NOT NULL）|
| IX_Messages_ConversationId | Messages | ConversationId | 一般 | 載入對話訊息 |
| IX_BotBindings_UserId | BotBindings | UserId | 一般 | 列出使用者的 Bot |
| IX_BotBindings_Platform | BotBindings | Platform | 一般 | Webhook 路由時快速定位 |
| IX_ExternalMessages_Platform_CreatedAt | ExternalMessages | (Platform, CreatedAt) | 複合 | Monitor 頁依平台+時間篩選 |
| IX_ExternalMessages_ExternalUserId | ExternalMessages | ExternalUserId | 一般 | 查詢特定外部使用者紀錄 |
| UX_TokenUsageStats | TokenUsageStats | (UserId, Date, ModelType, Source) | 複合唯一 | UPSERT 查找鍵 |
| IX_TokenUsageStats_UserId_Date | TokenUsageStats | (UserId, Date) | 複合 | 日期範圍統計查詢 |

---

## 6. 軟刪除設計

繼承 `SoftDeletableEntity` 的資料表：Personas、Conversations、BotBindings、PromptTemplates

**機制**：
- `AppDbContext.OnModelCreating` 為這四張表設定 `HasQueryFilter(x => !x.IsDeleted)`
- 所有 LINQ 查詢自動加上此條件，不需手動加 `WHERE`
- Service 層刪除時設定 `IsDeleted = true` 與 `DeletedAt = DateTime.UtcNow`，再 `SaveChangesAsync`
- 需要查詢「已刪除資料」時使用 `.IgnoreQueryFilters()`

**選擇不軟刪除的資料表**：
- Messages：跟隨 Conversation 生命週期，硬刪除即可
- PersonaVersions：快照紀錄，不需獨立刪除
- ExternalMessages：外部監控紀錄，不需軟刪除
- TokenUsageStats：分析資料，只增不刪

**為什麼用軟刪除而非直接 DELETE**：

1. **資料關聯性保護**  
   BotBindings 被 ExternalMessages 外鍵引用，直接硬刪除 Bot 會連帶刪除所有歷史對話監控紀錄（CASCADE），或導致外鍵孤立。軟刪除讓父資料邏輯消失但實體保留，子資料仍可正常讀取。

2. **可追蹤與稽核**  
   `DeletedAt` 記錄刪除時間，未來若需要稽核「誰在什麼時候刪了什麼」有據可查。直接 DELETE 後資料從資料庫消失，無法追溯。

3. **誤刪可還原**  
   BotBindings 儲存加密後的 Bot Token，一旦硬刪除即不可逆。軟刪除保留了管理員介入還原的可能性。

4. **全系統行為一致**  
   統一走軟刪除策略，避免不同資料表採用不同刪除機制，降低維護複雜度。Global Query Filter 確保所有查詢行為一致，不會因遺漏 `WHERE IsDeleted = 0` 而查出已刪資料。

---

## 7. ER 關聯圖

```
┌─────────┐
│  Users  │
└────┬────┘
     │ 1
     │───────────────────────────────────────────────────────┐
     │ 1                                                     │
     ├──────────────── ∞ ┌──────────────┐                   │
     │                   │   Personas   │                   │
     │                   └──────┬───────┘                   │
     │                          │ 1                          │
     │                          ├── ∞ ┌──────────────────┐  │
     │                          │     │ PersonaVersions  │  │
     │                          │     └──────────────────┘  │
     │                          │                            │
     ├──────────────── ∞ ┌──────┴───────┐                   │
     │                   │ Conversations│                   │
     │                   └──────┬───────┘                   │
     │                          │ 1                          │
     │                          └── ∞ ┌──────────┐          │
     │                               │ Messages │          │
     │                               └──────────┘          │
     │                                                      │
     ├──────────────── ∞ ┌─────────────┐                   │
     │                   │ BotBindings │◄──────────────────-┘
     │                   └──────┬──────┘  PersonaId (SET NULL)
     │                          │ 1
     │                          └── ∞ ┌──────────────────┐
     │                               │ ExternalMessages │
     │                               └──────────────────┘
     │
     ├──────────────── ∞ ┌─────────────────┐
     │                   │ PromptTemplates │
     │                   └─────────────────┘
     │
     └──────────────── ∞ ┌──────────────────┐
                         │ TokenUsageStats  │
                         └──────────────────┘
```

---

## 8. 資料流動流程

### 8-1. 使用者註冊 / 登入

```
前端送出 { email, password }
    │
    ▼
AuthService.RegisterAsync / LoginAsync
    │  Email 轉小寫查詢（防帳號列舉）
    │  BCrypt.HashPassword / BCrypt.Verify
    │  IsActive 檢查
    ▼
Users 資料表（寫入 / 讀取）
    │
    ▼
產生 JWT Token（包含 UserId、Email、Role）
    │
    ▼
回傳 { token, expiresAt, user }
```

---

### 8-2. 建立 / 編輯 Persona（含版本快照）

```
前端送出 { name, emoji, systemPrompt, promptSections }
    │
    ▼
PersonaService.CreateAsync / UpdateAsync
    │
    ├─ [新增] INSERT Personas（CurrentVersion = 1）
    │       INSERT PersonaVersions（Version = 1）
    │
    └─ [更新] UPDATE Personas（SystemPrompt、CurrentVersion += 1）
             INSERT PersonaVersions（Version = CurrentVersion，快照）
```

**版本回滾**：
```
PersonaService.RollbackVersionAsync(personaId, versionId)
    │
    ├─ 查詢 PersonaVersions 取得目標快照
    ├─ 建立新的 PersonaVersion（當前內容快照，Version += 1）
    └─ 更新 Personas（SystemPrompt = 目標版本的 SystemPrompt）
```

---

### 8-3. 後台 AI 對話（SSE 串流）

```
前端 POST /api/v1/ai/chat { conversationId, userMessage, modelOverride? }
    │
    ▼
AiController.Chat（SSE，text/event-stream）
    │
    ▼
AiService.ChatStreamAsync
    │
    ├─ 驗證 Conversation 歸屬（UserId 比對）
    ├─ 讀取 Persona.SystemPrompt（若有綁定）
    ├─ INSERT Messages（Role = "user"）
    │
    ├─ 呼叫 AI Provider API（HTTP SSE）
    │   逐 chunk 回傳給前端：{ type: "delta", content: "..." }
    │
    ├─ 累積完整回應
    │
    ├─ INSERT Messages（Role = "assistant"，完整內容）
    ├─ 更新 Conversations.Title（第一輪時 AI 自動命名）
    ├─ UPSERT TokenUsageStats（UserId, Date, ModelType, Source = "admin"）
    │
    └─ 回傳 { type: "done", usage: { tokensIn, tokensOut } }
```

---

### 8-4. AI 強化 Prompt

```
前端 POST /api/v1/personas/enhance-prompt { systemPrompt, instruction? }
    │
    ▼
PersonaService.EnhancePromptAsync
    │
    ├─ AiService.CompleteAsync（非串流，單次呼叫）
    │   systemPrompt = "你是 prompt engineering 專家..."
    │   userPrompt = 原始 Prompt（+ 強化方向 instruction）
    │
    └─ 回傳 { enhancedPrompt }（不寫入 DB，由使用者確認後手動儲存）
```

---

### 8-5. Bot 綁定（LINE / Discord）

```
前端送出 { platform, botName, botToken, channelSecret?, modelType, personaId? }
    │
    ▼
BotBindingService.CreateAsync
    │
    ├─ AesEncryptionHelper.Encrypt(botToken) → BotTokenEncrypted
    ├─ AesEncryptionHelper.Encrypt(channelSecret) → ChannelSecretEncrypted（LINE）
    ├─ 擷取後 4 碼 → TokenLastFour
    └─ INSERT BotBindings

啟用 / 停用 Toggle：
    UPDATE BotBindings SET IsEnabled = !IsEnabled

Rotate Token：
    AesEncryptionHelper.Encrypt(newToken) → BotTokenEncrypted（舊 Token 失效）
    擷取後 4 碼更新 → TokenLastFour
```

---

### 8-6. 外部 Bot 收到訊息（Webhook）

```
LINE / Discord Webhook → POST /api/v1/webhooks/{platform}
    │
    ▼
依 platform + Channel Token 查詢 BotBindings
    │
    ├─ 驗證 IsEnabled = true
    ├─ AesEncryptionHelper.Decrypt(BotTokenEncrypted)（驗證請求合法性）
    ├─ 讀取 Persona.SystemPrompt（若 PersonaId 不為 NULL）
    │
    ├─ 呼叫 AiService.CompleteAsync（非串流）
    │
    ├─ INSERT ExternalMessages × 2（Role = "user" + "assistant"）
    ├─ UPSERT TokenUsageStats（Source = "line" 或 "discord"）
    └─ 回傳 AI 回應給 LINE / Discord
```

---

### 8-7. Token 統計 UPSERT 邏輯

每次 AI 呼叫完成後執行：

```
查詢 TokenUsageStats WHERE
    UserId = {userId}
    AND Date = {today}
    AND ModelType = {modelType}
    AND Source = {source}

├─ 存在 → UPDATE：TokensIn += x, TokensOut += y, RequestCount += 1
└─ 不存在 → INSERT：TokensIn = x, TokensOut = y, RequestCount = 1
```

統計頁讀取時依 `(UserId, Date)` 索引快速取得日期範圍資料，再依 ModelType / Source 分組。

---

## 維護說明

每次新增以下內容時，請同步更新此文件：
- 新增資料表或欄位（含 Migration）
- 修改外鍵行為或索引
- 新增資料流動路徑（新 API 端點）
- 修改軟刪除或版本快照邏輯
