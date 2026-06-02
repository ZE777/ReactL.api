-- ============================================================
-- ReactL Prompt Studio - 初始資料庫結構
-- 版本：V001
-- 建立日期：2026-05-28
-- 說明：此檔案為人工可讀的 DDL 記錄，實際資料庫建立由 EF Core Migration 執行
--       對應 Migration：20260528073343_InitialCreate
-- ============================================================

USE ReactL;
GO

-- ── Users（系統使用者）──────────────────────────────────────────────────────
-- 管理後台操作人員帳號；密碼以 bcrypt 雜湊儲存，不儲存明文
CREATE TABLE Users (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    Email           NVARCHAR(256)    NOT NULL,              -- 登入 Email，唯一索引
    PasswordHash    NVARCHAR(500)    NOT NULL,              -- bcrypt 雜湊
    DisplayName     NVARCHAR(100)    NOT NULL,
    Role            NVARCHAR(20)     NOT NULL DEFAULT 'User', -- 'User' | 'Admin'
    IsActive        BIT              NOT NULL DEFAULT 1,    -- false 時登入被拒但資料保留
    LastLoginAt     DATETIME2        NULL,
    CreatedAt       DATETIME2        NOT NULL,
    UpdatedAt       DATETIME2        NOT NULL
);

CREATE UNIQUE INDEX IX_Users_Email ON Users(Email);

-- ── Personas（AI 角色）──────────────────────────────────────────────────────
-- 支援軟刪除、版本控制；UserId = NULL 表示系統內建角色
CREATE TABLE Personas (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    UserId          UNIQUEIDENTIFIER NULL,                  -- FK Users(Id)，NULL = 系統內建
    Name            NVARCHAR(100)    NOT NULL,
    Emoji           NVARCHAR(10)     NULL,
    SystemPrompt    NVARCHAR(MAX)    NOT NULL,              -- 當前使用中的完整 System Prompt
    PromptSections  NVARCHAR(MAX)    NULL,                  -- JSON：各區塊原始內容
    CurrentVersion  INT              NOT NULL DEFAULT 1,
    IsBuiltin       BIT              NOT NULL DEFAULT 0,    -- true 時不可刪除
    CreatedAt       DATETIME2        NOT NULL,
    UpdatedAt       DATETIME2        NOT NULL,
    IsDeleted       BIT              NOT NULL DEFAULT 0,    -- 軟刪除旗標
    DeletedAt       DATETIME2        NULL,

    CONSTRAINT FK_Personas_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE SET NULL
);

CREATE INDEX IX_Personas_UserId    ON Personas(UserId);
CREATE INDEX IX_Personas_IsDeleted ON Personas(IsDeleted);

-- ── PersonaVersions（角色版本快照）──────────────────────────────────────────
-- 每次修改 Persona 都產生一筆快照，可回滾至任意版本；Persona 刪除時 CASCADE DELETE
CREATE TABLE PersonaVersions (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    PersonaId       UNIQUEIDENTIFIER NOT NULL,              -- FK Personas(Id)
    Version         INT              NOT NULL,              -- 版本號，同 Persona 內唯一
    SystemPrompt    NVARCHAR(MAX)    NOT NULL,
    PromptSections  NVARCHAR(MAX)    NULL,
    ChangeNote      NVARCHAR(500)    NULL,                  -- 修改說明
    CreatedAt       DATETIME2        NOT NULL,

    CONSTRAINT FK_PersonaVersions_Personas FOREIGN KEY (PersonaId) REFERENCES Personas(Id) ON DELETE CASCADE
);

CREATE INDEX IX_PersonaVersions_PersonaId                    ON PersonaVersions(PersonaId);
CREATE UNIQUE INDEX UX_PersonaVersions_PersonaId_Version     ON PersonaVersions(PersonaId, Version);

-- ── PromptTemplates（Prompt 模板）──────────────────────────────────────────
-- 使用者的 Prompt 範本庫；支援軟刪除、標籤與分類篩選
CREATE TABLE PromptTemplates (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    UserId          UNIQUEIDENTIFIER NOT NULL,              -- FK Users(Id)
    Title           NVARCHAR(200)    NOT NULL,
    Content         NVARCHAR(MAX)    NOT NULL,
    Category        NVARCHAR(50)     NOT NULL DEFAULT '其他',
    Tags            NVARCHAR(500)    NULL,                  -- 逗號分隔的標籤字串
    UsageCount      INT              NOT NULL DEFAULT 0,    -- 累計使用次數
    CreatedAt       DATETIME2        NOT NULL,
    UpdatedAt       DATETIME2        NOT NULL,
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    DeletedAt       DATETIME2        NULL,

    CONSTRAINT FK_PromptTemplates_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE INDEX IX_PromptTemplates_UserId   ON PromptTemplates(UserId);
CREATE INDEX IX_PromptTemplates_Category ON PromptTemplates(Category);

-- ── BotBindings（Bot 平台綁定）──────────────────────────────────────────────
-- 記錄 LINE / Discord Bot 連線資訊；Token 以 AES-256-CBC 加密儲存
CREATE TABLE BotBindings (
    Id                      UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    UserId                  UNIQUEIDENTIFIER NOT NULL,      -- FK Users(Id)
    PersonaId               UNIQUEIDENTIFIER NULL,          -- FK Personas(Id)，刪除 Persona 時設 NULL
    Platform                NVARCHAR(20)     NOT NULL,      -- 'line' | 'discord'
    BotName                 NVARCHAR(100)    NOT NULL,
    BotTokenEncrypted       NVARCHAR(700)    NOT NULL,      -- AES-256-CBC 加密後 Base64
    ChannelSecretEncrypted  NVARCHAR(700)    NULL,          -- LINE 專用
    TokenLastFour           NVARCHAR(4)      NOT NULL,      -- 明文後 4 碼，列表頁顯示用（避免 N 次解密）
    ModelType               NVARCHAR(50)     NOT NULL,      -- AI 模型識別碼
    IsEnabled               BIT              NOT NULL DEFAULT 1,
    CreatedAt               DATETIME2        NOT NULL,
    UpdatedAt               DATETIME2        NOT NULL,
    IsDeleted               BIT              NOT NULL DEFAULT 0,
    DeletedAt               DATETIME2        NULL,

    CONSTRAINT FK_BotBindings_Users    FOREIGN KEY (UserId)    REFERENCES Users(Id)    ON DELETE CASCADE,
    CONSTRAINT FK_BotBindings_Personas FOREIGN KEY (PersonaId) REFERENCES Personas(Id) ON DELETE SET NULL
);

CREATE INDEX IX_BotBindings_UserId   ON BotBindings(UserId);
CREATE INDEX IX_BotBindings_Platform ON BotBindings(Platform);

-- ── Conversations（對話記錄）────────────────────────────────────────────────
-- 管理後台發起的測試對話；支援軟刪除、釘選、公開分享
CREATE TABLE Conversations (
    Id          UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    UserId      UNIQUEIDENTIFIER NOT NULL,                  -- FK Users(Id)
    PersonaId   UNIQUEIDENTIFIER NULL,                      -- FK Personas(Id)，刪除時設 NULL
    Title       NVARCHAR(200)    NOT NULL DEFAULT '新對話',
    ModelType   NVARCHAR(50)     NOT NULL,
    IsPinned    BIT              NOT NULL DEFAULT 0,
    IsPublic    BIT              NOT NULL DEFAULT 0,
    ShareSlug   NVARCHAR(20)     NULL,                      -- 公開分享短碼，非 NULL 時唯一
    CreatedAt   DATETIME2        NOT NULL,
    UpdatedAt   DATETIME2        NOT NULL,
    IsDeleted   BIT              NOT NULL DEFAULT 0,
    DeletedAt   DATETIME2        NULL,

    CONSTRAINT FK_Conversations_Users    FOREIGN KEY (UserId)    REFERENCES Users(Id)    ON DELETE CASCADE,
    CONSTRAINT FK_Conversations_Personas FOREIGN KEY (PersonaId) REFERENCES Personas(Id) ON DELETE SET NULL
);

CREATE INDEX IX_Conversations_UserId ON Conversations(UserId);
-- FILTERED UNIQUE INDEX：ShareSlug 非 NULL 時才套用唯一性約束
CREATE UNIQUE INDEX IX_Conversations_ShareSlug ON Conversations(ShareSlug) WHERE [ShareSlug] IS NOT NULL;

-- ── Messages（對話訊息）──────────────────────────────────────────────────────
-- 硬刪除（無 IsDeleted）；Conversation 刪除時 CASCADE DELETE
CREATE TABLE Messages (
    Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    ConversationId  UNIQUEIDENTIFIER NOT NULL,              -- FK Conversations(Id)
    Role            NVARCHAR(20)     NOT NULL,              -- 'user' | 'assistant' | 'system'
    Content         NVARCHAR(MAX)    NOT NULL,
    TokensIn        INT              NOT NULL DEFAULT 0,    -- 本次請求消耗的輸入 Token
    TokensOut       INT              NOT NULL DEFAULT 0,    -- 本次請求消耗的輸出 Token
    CreatedAt       DATETIME2        NOT NULL,

    CONSTRAINT FK_Messages_Conversations FOREIGN KEY (ConversationId) REFERENCES Conversations(Id) ON DELETE CASCADE
);

CREATE INDEX IX_Messages_ConversationId ON Messages(ConversationId);

-- ── ExternalMessages（外部平台訊息）────────────────────────────────────────
-- LINE / Discord Bot 收到與回傳的訊息記錄；Platform 冗餘存放以加速 Monitor 頁篩選
CREATE TABLE ExternalMessages (
    Id                UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    BotBindingId      UNIQUEIDENTIFIER NOT NULL,            -- FK BotBindings(Id)
    Platform          NVARCHAR(20)     NOT NULL,            -- 冗餘欄位（避免 JOIN），'line' | 'discord'
    ExternalUserId    NVARCHAR(200)    NOT NULL,            -- LINE userId / Discord snowflake
    ExternalChannelId NVARCHAR(200)    NULL,                -- Discord 頻道 ID
    Role              NVARCHAR(20)     NOT NULL,            -- 'user' | 'assistant'
    Content           NVARCHAR(MAX)    NOT NULL,
    TokensIn          INT              NOT NULL DEFAULT 0,
    TokensOut         INT              NOT NULL DEFAULT 0,
    CreatedAt         DATETIME2        NOT NULL,

    CONSTRAINT FK_ExternalMessages_BotBindings FOREIGN KEY (BotBindingId) REFERENCES BotBindings(Id) ON DELETE CASCADE
);

CREATE INDEX IX_ExternalMessages_BotBindingId        ON ExternalMessages(BotBindingId);
CREATE INDEX IX_ExternalMessages_ExternalUserId      ON ExternalMessages(ExternalUserId);
-- 複合索引：Monitor 頁常用 Platform + 時間範圍篩選
CREATE INDEX IX_ExternalMessages_Platform_CreatedAt  ON ExternalMessages(Platform, CreatedAt);

-- ── TokenUsageStats（Token 用量統計）────────────────────────────────────────
-- 每日彙總記錄（每使用者 × 日期 × 模型 × 來源唯一一筆），用於 UPSERT
CREATE TABLE TokenUsageStats (
    Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    UserId       UNIQUEIDENTIFIER NOT NULL,                 -- FK Users(Id)
    Date         DATE             NOT NULL,                 -- 統計日期（UTC）
    ModelType    NVARCHAR(50)     NOT NULL,                 -- AI 模型識別碼
    Source       NVARCHAR(20)     NOT NULL,                 -- 'admin' | 'web' | 'line' | 'discord'
    TokensIn     INT              NOT NULL DEFAULT 0,
    TokensOut    INT              NOT NULL DEFAULT 0,
    RequestCount INT              NOT NULL DEFAULT 0,
    CreatedAt    DATETIME2        NOT NULL,

    CONSTRAINT FK_TokenUsageStats_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE INDEX IX_TokenUsageStats_UserId_Date                    ON TokenUsageStats(UserId, Date);
-- UPSERT 用唯一索引：同一使用者在同一天同一模型同一來源只有一筆
CREATE UNIQUE INDEX UX_TokenUsageStats_UserId_Date_Model_Source ON TokenUsageStats(UserId, Date, ModelType, Source);
GO
