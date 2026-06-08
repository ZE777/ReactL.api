-- ============================================================
-- ReactL Prompt Studio - BotBindings 憑證驗證狀態一般化
-- 版本：V007
-- 建立日期：2026-06-05
-- 說明：
--   將 V006 新增的 Discord 專用欄位 DiscordCommandRegistered 一般化為
--   CredentialValid，供 LINE 與 Discord 共用同一個「憑證/設定是否驗證通過」狀態：
--   - Discord：/chat 指令註冊成功 = 憑證（Token / Application ID）有效
--   - LINE：呼叫 GET /v2/bot/info 成功 = Channel Access Token 有效
--   - NULL = 尚未驗證；1 = 有效；0 = 無效（需修正後重存）
--   ★ 冪等且相容兩種狀態：
--     - 已套用 V006（欄位存在）→ 以 sp_rename 改名，保留既有資料
--     - 未套用 V006（欄位不存在）→ 直接新增 CredentialValid
--     - 已是 CredentialValid → 不動作
-- ============================================================

USE ReactL;
GO

IF COL_LENGTH('BotBindings', 'CredentialValid') IS NULL
BEGIN
    IF COL_LENGTH('BotBindings', 'DiscordCommandRegistered') IS NOT NULL
        EXEC sp_rename 'BotBindings.DiscordCommandRegistered', 'CredentialValid', 'COLUMN';
    ELSE
        ALTER TABLE BotBindings ADD CredentialValid BIT NULL;
END
GO
