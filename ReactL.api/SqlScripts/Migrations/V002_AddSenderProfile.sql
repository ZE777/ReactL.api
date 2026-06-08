-- ================================================================
-- V002 - 新增外部使用者暱稱與頭像欄位到 ExternalMessages 表
-- 建立日期：2026-06-03
-- 說明：ExternalMessage 實體新增 SenderName / SenderAvatarUrl 屬性，
--       對應從 LINE / Discord Profile API 取得的使用者顯示名稱與頭像。
--       執行一次即可，欄位皆為 NULL 不影響現有資料列。
-- ================================================================

ALTER TABLE [ExternalMessages]
ADD [SenderName]      NVARCHAR(200) NULL,
    [SenderAvatarUrl] NVARCHAR(500) NULL;

PRINT 'V002 完成：ExternalMessages 新增 SenderName、SenderAvatarUrl 欄位';