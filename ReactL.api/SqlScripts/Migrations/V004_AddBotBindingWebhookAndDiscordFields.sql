-- V004：BotBindings 加入每筆 Bot 專屬 Webhook BaseUrl 與 Discord 驗簽欄位
-- 原本 DiscordSettings（appsettings/User Secrets）的 PublicKey / ApplicationId 改為存在 BotBinding，
-- 支援多組 Discord Bot 各自使用不同 Application，互不干擾。
-- WebhookBaseUrl 讓不同伺服器代管的 Bot 可以各自指定 Webhook 基礎網址。

ALTER TABLE BotBindings ADD WebhookBaseUrl NVARCHAR(500) NULL;
ALTER TABLE BotBindings ADD DiscordApplicationId NVARCHAR(50) NULL;
ALTER TABLE BotBindings ADD DiscordPublicKey NVARCHAR(100) NULL;