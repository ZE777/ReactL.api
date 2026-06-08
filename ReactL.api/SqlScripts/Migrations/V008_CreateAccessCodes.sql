-- ============================================================
-- ReactL Prompt Studio - 新增存取碼（邀請碼）與用量表 + Users 首登改密旗標
-- 版本：V008
-- 建立日期：2026-06-05
-- 說明：
--   1. AccessCodes：前台公開聊天的存取碼（邀請碼），含每日 token 上限與到期/啟用控制。
--      邀請連結即「前台網址 + ?code=Code」。
--   2. AccessCodeUsages：每碼每日用量彙總（UPSERT），供每日配額檢查與後台顯示。
--   3. Users 新增 MustChangePassword：種子 Admin 首次登入後強制改密用。
--   ★ 冪等：以 IF NOT EXISTS / COL_LENGTH 包裹，重複執行不會報錯。
-- ============================================================

USE ReactL;
GO

-- 1) AccessCodes ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AccessCodes')
BEGIN
    CREATE TABLE AccessCodes (
        Id              UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        Code            NVARCHAR(32)     NOT NULL,              -- 隨機產生，唯一
        Label           NVARCHAR(100)    NULL,                 -- 備註（給誰／用途）
        DailyTokenLimit INT              NOT NULL DEFAULT 0,   -- 每日 token 上限；0 = 不限制
        ExpiresAt       DATETIME2        NULL,                 -- 到期時間；NULL = 永不過期
        IsActive        BIT              NOT NULL DEFAULT 1,
        CreatedAt       DATETIME2        NOT NULL,
        UpdatedAt       DATETIME2        NOT NULL
    );

    -- 存取碼全域唯一，供前台以碼查詢
    CREATE UNIQUE INDEX UX_AccessCodes_Code ON AccessCodes(Code);
END
GO

-- 2) AccessCodeUsages -------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AccessCodeUsages')
BEGIN
    CREATE TABLE AccessCodeUsages (
        Id           UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        AccessCodeId UNIQUEIDENTIFIER NOT NULL,               -- FK AccessCodes(Id)
        [Date]       DATE             NOT NULL,               -- 統計日期（台灣時間）
        TokensIn     INT              NOT NULL DEFAULT 0,
        TokensOut    INT              NOT NULL DEFAULT 0,
        RequestCount INT              NOT NULL DEFAULT 0,
        CreatedAt    DATETIME2        NOT NULL,

        CONSTRAINT FK_AccessCodeUsages_AccessCodes
            FOREIGN KEY (AccessCodeId) REFERENCES AccessCodes(Id) ON DELETE CASCADE
    );

    -- 同一存取碼同一天只有一筆彙總記錄，供 UPSERT 與每日配額查詢
    CREATE UNIQUE INDEX UX_AccessCodeUsages_AccessCodeId_Date
        ON AccessCodeUsages(AccessCodeId, [Date]);
END
GO

-- 3) Users.MustChangePassword ----------------------------------------------
IF COL_LENGTH('Users', 'MustChangePassword') IS NULL
BEGIN
    ALTER TABLE Users ADD MustChangePassword BIT NOT NULL DEFAULT 0;
END
GO

PRINT 'V008 完成：AccessCodes、AccessCodeUsages 建立；Users 新增 MustChangePassword';
