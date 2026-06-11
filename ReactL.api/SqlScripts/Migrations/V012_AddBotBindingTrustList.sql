-- ============================================================
-- ReactL Prompt Studio - BotBindings 新增「信任系統成員名單」
-- 版本：V012
-- 建立日期：2026-06-11
-- 說明：
--   為 Discord Bot 的「信任系統」新增一個欄位 TrustedUsersJson：
--     以 JSON 陣列儲存成員名單，每筆結構
--       [{ "id", "label", "tier", "systemRole", "grantedBy", "grantedAt" }]
--     - id：成員 Discord User ID
--     - label：名稱／顯示稱呼
--     - tier：關係（自訂情感標籤，例如「主人」「爹地」「朋友」），給角色語氣參考
--     - systemRole：系統角色（功能權限）："owner"=主人/管理者、"trusted"=信任者
--       → 系統角色為 owner 的成員（可多人）才能維護名單
--   兩條維護路徑共用此欄位：
--     - 路徑 1：後台直接 CRUD（App 登入授權，含指定/變更系統角色、設第一個主人）。
--     - 路徑 2：Discord 對話中由「現任主人」要求，加入前於伺服器跳二次確認；
--       對話路徑加入者一律為 trusted（不開放用聊天提權為 owner）。
--   ★ 冪等：以 COL_LENGTH 檢查，重複執行不會報錯。
-- ============================================================

USE ReactL;
GO

IF COL_LENGTH('BotBindings', 'TrustedUsersJson') IS NULL
BEGIN
    ALTER TABLE BotBindings
        ADD TrustedUsersJson NVARCHAR(MAX) NULL;
END
GO

PRINT 'V012 完成：BotBindings 新增 TrustedUsersJson（信任系統成員名單，含 systemRole）';