-- ============================================================
-- ReactL Prompt Studio - 新增 Persona 內建分組欄位
-- 版本：V003
-- 建立日期：2026-06-03
-- 說明：
--   1. 新增 BuiltinGroup 欄位區分角色來源
--      'Official' = 系統內建（取代舊版 UserId IS NULL 判斷）
--      'User'     = 使用者自訂（預設值）
--   2. UserId 改為 NOT NULL：Official Persona 需指定系統用戶 ID
--      ★ 前提：若資料表有 UserId = NULL 的現有記錄，
--        請先執行：UPDATE Personas SET UserId = '<SYSTEM_USER_ID>' WHERE UserId IS NULL
--        再執行 Step 4~6，否則 ALTER COLUMN 會失敗
--   3. FK ON DELETE 改為 CASCADE（原為 SET NULL）：
--      使用者刪除時連帶清除其自訂 Persona
-- ============================================================

USE ReactL;
GO

-- ── Step 1: 新增 BuiltinGroup 欄位（預設 'User'）────────────────────────────
ALTER TABLE Personas
    ADD BuiltinGroup NVARCHAR(50) NOT NULL
        CONSTRAINT DF_Personas_BuiltinGroup DEFAULT 'User';
GO

-- ── Step 2: 補填現有 NULL UserId 記錄的 BuiltinGroup ───────────────────────
-- 若資料表有以舊格式存入的系統內建 Persona（UserId = NULL），補標為 'Official'
UPDATE Personas
SET BuiltinGroup = 'Official'
WHERE UserId IS NULL;
GO

-- ── Step 3: 加入 BuiltinGroup CHECK 約束 ───────────────────────────────────
ALTER TABLE Personas
    ADD CONSTRAINT CK_Personas_BuiltinGroup
        CHECK (BuiltinGroup IN ('Official', 'User'));
GO

-- ── Step 4: 移除舊 FK（帶有 ON DELETE SET NULL 行為）─────────────────────
-- 實際約束名稱由 EF Core 自動產生，非 V001 手動命名
ALTER TABLE Personas
    DROP CONSTRAINT FK_Personas_Users_UserId;
GO

-- ── Step 5: 將 UserId 改為 NOT NULL ────────────────────────────────────────
-- ALTER COLUMN 前必須先移除依賴此欄位的索引，完成後重建
-- 若仍有 NULL 值此步驟會失敗，請確認 Step 2 已正確更新所有記錄
DROP INDEX IX_Personas_UserId ON Personas;
GO

ALTER TABLE Personas
    ALTER COLUMN UserId UNIQUEIDENTIFIER NOT NULL;
GO

CREATE INDEX IX_Personas_UserId ON Personas(UserId);
GO

-- ── Step 6: 重新建立 FK（ON DELETE NO ACTION）──────────────────────────────
-- 注意：不使用 CASCADE，因為 Users→Personas→Conversations 會形成多重 cascade path，
--       SQL Server 禁止此設定（Error 1785）；刪除使用者前需在 application layer 先清除其 Persona
ALTER TABLE Personas
    ADD CONSTRAINT FK_Personas_Users
        FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE NO ACTION;
GO
