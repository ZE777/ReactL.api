-- ============================================================
-- ReactL Prompt Studio - 新增 Ai 金鑰表（混合 BYOK）
-- 版本：V005
-- 建立日期：2026-06-05
-- 說明：
--   建立 AiKeys 表，統一存放「系統預設 Key」與「使用者自帶 Key」。
--   - 系統預設：UserId = 系統用戶(11111111-...)、IsSystem = 1，
--     由 API 啟動時的 seeder 從 appsettings 加密寫入（不在此腳本種子，
--     因為金鑰必須由 C# 的 AesEncryptionHelper 加密才相容）。
--   - 使用者自帶：UserId = 該使用者，覆蓋系統預設以隔離額度。
--   金鑰以 AES-256-CBC 加密儲存，前端僅顯示後 4 碼。
--   ★ 冪等：以 IF NOT EXISTS 包裹，重複執行不會報錯。
-- ============================================================

USE ReactL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AiKeys')
BEGIN
    CREATE TABLE AiKeys (
        Id            UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
        UserId        UNIQUEIDENTIFIER NOT NULL,              -- FK Users(Id)，系統預設 Key 指向系統用戶
        ProviderId    NVARCHAR(50)     NOT NULL,              -- 對應 AiSettings.Providers[].Id（如 'groq'）
        EncryptedKey  NVARCHAR(700)    NOT NULL,              -- AES-256-CBC 密文（Base64）
        KeyLastFour   NVARCHAR(8)      NOT NULL,              -- 金鑰後 4 碼，供前端顯示
        IsActive      BIT              NOT NULL DEFAULT 1,
        IsSystem      BIT              NOT NULL DEFAULT 0,    -- 1 = 系統預設 Key（fallback 來源）
        CreatedAt     DATETIME2        NOT NULL,
        UpdatedAt     DATETIME2        NOT NULL,

        CONSTRAINT FK_AiKeys_Users FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
    );

    -- 每位使用者在同一供應商最多一把 Key（供 UPSERT 與解析）
    CREATE UNIQUE INDEX UX_AiKeys_UserId_ProviderId ON AiKeys(UserId, ProviderId);
END
GO
