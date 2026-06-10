-- ============================================================
-- ReactL Prompt Studio - 種子後台管理員帳號
-- 檔案：Seed/Seed_AdminUser.sql
-- 說明：
--   於建置 DB 時直接建立可登入的 Admin 帳號（Role='Admin'）。
--   與 C# AdminSeeder 互補：此腳本為「主要途徑」，C# 啟動種子為「補救/fallback」。
--   兩者皆以 Email = admin@reactl.local 判斷，誰先建好另一個就跳過，不會重複。
--
--   密碼：Admin@12345（下方為其 BCrypt 雜湊；SQL 無法即時做 BCrypt，故預先以
--         專案 BCrypt.Net-Next 產生）。MustChangePassword=1 → 首登強制改密，
--         因此此雜湊僅單次可用，改密後即失效。
--   ★ 冪等：IF NOT EXISTS 包裹，重複執行不會報錯、不會重複建立。
--   ⚠️ 此檔含預設密碼的雜湊，正式環境請於首登後立即改密；或改走 C# 啟動種子
--      （密碼由設定檔即時雜湊、不落地）。
-- ============================================================

USE ReactL;
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @AdminId      UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222222';
DECLARE @AdminEmail   NVARCHAR(256)    = N'admin@reactl.local';
DECLARE @SystemUserId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @Now          DATETIME2        = SYSDATETIME();

-- ── 1) 建立 Admin 帳號（僅在該 Email 尚不存在時）──────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = @AdminEmail)
BEGIN
    INSERT INTO Users (Id, Email, PasswordHash, DisplayName, Role, IsActive, MustChangePassword, CreatedAt, UpdatedAt)
    VALUES (
        @AdminId,
        @AdminEmail,
        N'$2a$11$6QU4jUQlAD8Tz2lbmtfv7eY1vwFRuY0Tz6zC89SO80ru6UXZLOIyC', -- BCrypt('Admin@12345')
        N'管理員',
        N'Admin',
        1,   -- IsActive
        1,   -- MustChangePassword：首登強制改密
        @Now, @Now
    );
    PRINT '已建立種子 Admin：admin@reactl.local（預設密碼 Admin@12345，首登強制改密）';
END
ELSE
BEGIN
    -- 取既有同 Email 帳號的 Id，供下方金鑰綁定使用
    SELECT @AdminId = Id FROM Users WHERE Email = @AdminEmail;
    PRINT 'Admin（admin@reactl.local）已存在，略過帳號建立';
END

-- ── 2) 將系統預設 AI 金鑰複製綁定給 Admin（避免首登被鎖在「AI 金鑰」頁）────────
--    對齊 C# AdminSeeder.GrantSystemKeysToUserAsync：重用系統 Key 的 AES 密文（同把金鑰），
--    以 IsSystem=0 綁給 Admin。冪等：Admin 在該供應商已有 Key 則略過。
--    註：僅綁定「執行此腳本當下已存在的系統 Key」；系統 Key 由 C# 啟動種子從 appsettings
--        加密寫入，若尚未產生則此段不會綁到任何 Key（屆時由 C# 流程或手動設定補上）。
INSERT INTO AiKeys (Id, UserId, ProviderId, EncryptedKey, KeyLastFour, IsActive, IsSystem, CreatedAt, UpdatedAt)
SELECT NEWID(), @AdminId, sk.ProviderId, sk.EncryptedKey, sk.KeyLastFour, 1, 0, @Now, @Now
FROM AiKeys sk
WHERE sk.UserId = @SystemUserId
  AND sk.IsSystem = 1
  AND sk.IsActive = 1
  AND NOT EXISTS (
        SELECT 1 FROM AiKeys a
        WHERE a.UserId = @AdminId AND a.ProviderId = sk.ProviderId
  );

COMMIT TRANSACTION;
GO

-- 驗證
SELECT Email, Role, IsActive, MustChangePassword FROM Users WHERE Email = N'admin@reactl.local';
SELECT ProviderId, KeyLastFour, IsSystem FROM AiKeys
WHERE UserId = '22222222-2222-2222-2222-222222222222';
GO
