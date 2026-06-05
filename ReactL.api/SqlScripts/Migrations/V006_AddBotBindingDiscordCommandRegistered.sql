-- ============================================================
-- ReactL Prompt Studio - BotBindings 加入 Discord 指令註冊狀態
-- 版本：V006
-- 建立日期：2026-06-05
-- 說明：
--   新增 DiscordCommandRegistered 欄位，持久化 Discord /chat 指令的
--   自動註冊結果，讓後台即使重新整理也能標示該筆 Bot 為「無效」。
--   - NULL  = 非 Discord 平台或尚未觸發註冊
--   - 1(true) = 註冊成功
--   - 0(false) = 註冊失敗（資料無效，需修正 Token / ApplicationId 後重存）
--   ★ 冪等：以 IF NOT EXISTS 包裹，重複執行不會報錯。
-- ============================================================

USE ReactL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('BotBindings') AND name = 'DiscordCommandRegistered'
)
BEGIN
    ALTER TABLE BotBindings ADD DiscordCommandRegistered BIT NULL;
END
GO