-- ============================================================
-- ReactL Prompt Studio - 新增前台公開聊天記錄表（後台 Admin 對話監控用）
-- 版本：V010
-- 建立日期：2026-06-05
-- 說明：
--   PublicChatLogs：前台公開聊天（存取碼進來的訪客）每則 user / assistant 訊息記錄，
--   供後台 Admin「前台聊天監控」頁檢視完整對話內容與 token 用量。
--   - 以 SessionId 將同一訪客的連續對話分組（前端 localStorage 產生，X-Chat-Session 帶入）。
--   - AccessCodeId 為 FK → AccessCodes，存取碼刪除時 SET NULL（保留歷史，顯示改用 AccessCodeText 快照）。
--   - 逾 PublicChatSettings.LogRetentionDays（預設 30）天由背景服務清除。
--   ★ 冪等：以 IF NOT EXISTS 包裹，重複執行不會報錯。
-- ============================================================

USE ReactL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PublicChatLogs')
BEGIN
    CREATE TABLE PublicChatLogs (
        Id             UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        SessionId      NVARCHAR(64)     NOT NULL,              -- 前端產生，分組同一訪客對話
        AccessCodeId   UNIQUEIDENTIFIER NULL,                 -- FK AccessCodes(Id)；匿名為 NULL
        AccessCodeText NVARCHAR(32)     NULL,                 -- 存取碼字串快照（即使碼被刪除仍可辨識）
        Role           NVARCHAR(20)     NOT NULL,             -- user / assistant
        Content        NVARCHAR(MAX)    NOT NULL,             -- 完整訊息內容
        ModelType      NVARCHAR(50)     NULL,                 -- providerId:modelId
        PersonaId      UNIQUEIDENTIFIER NULL,                 -- 使用的公開角色（如有）
        TokensIn       INT              NOT NULL DEFAULT 0,
        TokensOut      INT              NOT NULL DEFAULT 0,
        CreatedAt      DATETIME2        NOT NULL,

        -- 存取碼刪除時保留聊天記錄，僅將 AccessCodeId 設為 NULL
        CONSTRAINT FK_PublicChatLogs_AccessCodes
            FOREIGN KEY (AccessCodeId) REFERENCES AccessCodes(Id) ON DELETE SET NULL
    );

    -- 監控頁以 SessionId 分組列出對話
    CREATE INDEX IX_PublicChatLogs_SessionId ON PublicChatLogs(SessionId);

    -- 保留清除背景服務以 CreatedAt 範圍刪除逾期記錄
    CREATE INDEX IX_PublicChatLogs_CreatedAt ON PublicChatLogs(CreatedAt);
END
GO

PRINT 'V010 完成：PublicChatLogs 建立';