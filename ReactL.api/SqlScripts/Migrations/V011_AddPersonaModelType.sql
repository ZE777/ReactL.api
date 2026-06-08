-- ============================================================
-- ReactL Prompt Studio - Personas 新增 ModelType（前台公開聊天角色的 AI 模型）
-- 版本：V011
-- 建立日期：2026-06-05
-- 說明：
--   Personas 新增 ModelType 欄位，讓 Admin 為每個「開放前台」的角色各自指定
--   前台公開聊天要使用的 AI 模型（格式 providerId:modelId）。
--   後台聊天的模型由對話本身決定，不受此欄位影響。
--   既有資料預設為 groq:llama-3.3-70b-versatile（沿用原本前台寫死的模型）。
--   ★ 冪等：以 COL_LENGTH 檢查，重複執行不會報錯。
-- ============================================================

USE ReactL;
GO

IF COL_LENGTH('Personas', 'ModelType') IS NULL
BEGIN
    ALTER TABLE Personas
        ADD ModelType NVARCHAR(50) NOT NULL
        CONSTRAINT DF_Personas_ModelType DEFAULT 'groq:llama-3.3-70b-versatile';
END
GO

PRINT 'V011 完成：Personas 新增 ModelType';