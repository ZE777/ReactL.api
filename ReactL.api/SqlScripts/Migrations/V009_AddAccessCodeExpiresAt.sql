-- ============================================================
-- ReactL Prompt Studio - 補上 AccessCodes.ExpiresAt 欄位
-- 版本：V009
-- 建立日期：2026-06-05
-- 說明：
--   V008 的 CREATE TABLE 已含 ExpiresAt，但該腳本以 IF NOT EXISTS(表) 包裹（冪等）。
--   若 AccessCodes 表在「ExpiresAt 加入 V008 之前」就已建立，重跑 V008 會整段跳過、
--   不會補欄位，導致後端查詢報「Invalid column name 'ExpiresAt'」。
--   本腳本針對既有環境補上該欄位（到期時間；NULL = 永不過期）。
--   ★ 冪等：欄位已存在則不動作。
-- ============================================================

USE ReactL;
GO

IF COL_LENGTH('AccessCodes', 'ExpiresAt') IS NULL
BEGIN
    ALTER TABLE AccessCodes ADD ExpiresAt DATETIME2 NULL;
END
GO
