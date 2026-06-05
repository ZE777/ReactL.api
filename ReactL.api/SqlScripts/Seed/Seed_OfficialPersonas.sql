-- ============================================================
-- ReactL Prompt Studio - 種子資料：系統內建 Official Persona
-- 檔案：SqlScripts/Seed/Seed_OfficialPersonas.sql
-- 建立日期：2026-06-04
-- 說明：
--   建立 5 個系統內建角色（工程師 / 寫作助手 / 翻譯員 / UX 顧問 / 產品經理）。
--   這些角色 BuiltinGroup='Official'、IsBuiltin=1：
--     - 後台 GetListAsync：所有使用者皆可見（唯讀，非本人無寫入權）
--     - 前台 GetPublicPersonasAsync：IsBuiltin=1 → 公開聊天室可選用
--
--   前提：V003 之後 Personas.UserId 為 NOT NULL + FK，Official Persona 必須
--   掛在一個使用者底下。本腳本會先建立一個「系統用戶」(IsActive=0、無法登入)
--   作為這些角色的擁有者。
--
--   ★ 冪等：全部以固定 GUID + IF NOT EXISTS 包裹，重複執行不會重複插入。
--   SystemPrompt 依前端 assembleSystemPrompt 格式組裝；PromptSections 為對應 JSON。
-- ============================================================

USE ReactL;
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

-- ── 固定 GUID 常數 ─────────────────────────────────────────────────────────
DECLARE @SystemUserId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';

DECLARE @P_Eng   UNIQUEIDENTIFIER = 'A1000000-0000-0000-0000-000000000001';
DECLARE @P_Write UNIQUEIDENTIFIER = 'A1000000-0000-0000-0000-000000000002';
DECLARE @P_Trans UNIQUEIDENTIFIER = 'A1000000-0000-0000-0000-000000000003';
DECLARE @P_Ux    UNIQUEIDENTIFIER = 'A1000000-0000-0000-0000-000000000004';
DECLARE @P_Pm    UNIQUEIDENTIFIER = 'A1000000-0000-0000-0000-000000000005';

DECLARE @V_Eng   UNIQUEIDENTIFIER = 'B1000000-0000-0000-0000-000000000001';
DECLARE @V_Write UNIQUEIDENTIFIER = 'B1000000-0000-0000-0000-000000000002';
DECLARE @V_Trans UNIQUEIDENTIFIER = 'B1000000-0000-0000-0000-000000000003';
DECLARE @V_Ux    UNIQUEIDENTIFIER = 'B1000000-0000-0000-0000-000000000004';
DECLARE @V_Pm    UNIQUEIDENTIFIER = 'B1000000-0000-0000-0000-000000000005';

DECLARE @Now DATETIME2 = SYSDATETIME();
DECLARE @NL  NVARCHAR(4) = NCHAR(10) + NCHAR(10);   -- 區塊分隔（與前端 \n\n 一致）

-- ── 系統用戶（不可登入的角色擁有者）────────────────────────────────────────
-- PasswordHash 為結構合法但不對應任何密碼的 bcrypt 值 → BCrypt.Verify 一律回 false。
-- 另設 IsActive=0 作為第二道防線（即便雜湊比對通過，登入仍會被 Forbidden 擋下）。
IF NOT EXISTS (SELECT 1 FROM Users WHERE Id = @SystemUserId)
BEGIN
    INSERT INTO Users (Id, Email, PasswordHash, DisplayName, Role, IsActive, CreatedAt, UpdatedAt)
    VALUES (
        @SystemUserId,
        N'system@reactl.internal',
        N'$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWa', -- 無效密碼（不可登入）
        N'系統',
        N'User',
        0,
        @Now, @Now
    );
END

-- ── 角色 1：工程師 ─────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Personas WHERE Id = @P_Eng)
BEGIN
    DECLARE @sp_eng NVARCHAR(MAX) =
        N'你是一位資深的軟體工程師' + @NL +
        N'背景：精通多種程式語言與軟體架構設計，熟悉前後端開發與測試的最佳實務' + @NL +
        N'任務：協助使用者解決程式問題、審查程式碼、解釋技術概念並提供可運作的範例' + @NL +
        N'回覆格式：先用一兩句說明思路，再給出程式碼區塊，必要時補充注意事項' + @NL +
        N'限制：不杜撰不存在的 API 或函式；不確定時明確說明；提供的範例需可直接執行';
    DECLARE @ps_eng NVARCHAR(MAX) =
        N'{"role":"你是一位資深的軟體工程師","background":"精通多種程式語言與軟體架構設計，熟悉前後端開發與測試的最佳實務","task":"協助使用者解決程式問題、審查程式碼、解釋技術概念並提供可運作的範例","format":"先用一兩句說明思路，再給出程式碼區塊，必要時補充注意事項","constraints":"不杜撰不存在的 API 或函式；不確定時明確說明；提供的範例需可直接執行","examples":""}';

    INSERT INTO Personas (Id, UserId, BuiltinGroup, Name, Emoji, SystemPrompt, PromptSections, CurrentVersion, IsBuiltin, CreatedAt, UpdatedAt, IsDeleted, DeletedAt)
    VALUES (@P_Eng, @SystemUserId, N'Official', N'工程師', N'👨‍💻', @sp_eng, @ps_eng, 1, 1, @Now, @Now, 0, NULL);

    INSERT INTO PersonaVersions (Id, PersonaId, Version, SystemPrompt, PromptSections, ChangeNote, CreatedAt)
    VALUES (@V_Eng, @P_Eng, 1, @sp_eng, @ps_eng, N'系統初始建立', @Now);
END

-- ── 角色 2：寫作助手 ───────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Personas WHERE Id = @P_Write)
BEGIN
    DECLARE @sp_write NVARCHAR(MAX) =
        N'你是一位專業的中文寫作助理' + @NL +
        N'背景：擅長各類文體，包括行銷文案、商務信件、報告與創意寫作' + @NL +
        N'任務：協助使用者潤飾文字、調整語氣、擴寫或精簡內容' + @NL +
        N'回覆格式：直接提供修改後的文字，並在最後用一兩句說明調整重點' + @NL +
        N'限制：保留原意，不擅自增加未提供的事實資訊';
    DECLARE @ps_write NVARCHAR(MAX) =
        N'{"role":"你是一位專業的中文寫作助理","background":"擅長各類文體，包括行銷文案、商務信件、報告與創意寫作","task":"協助使用者潤飾文字、調整語氣、擴寫或精簡內容","format":"直接提供修改後的文字，並在最後用一兩句說明調整重點","constraints":"保留原意，不擅自增加未提供的事實資訊","examples":""}';

    INSERT INTO Personas (Id, UserId, BuiltinGroup, Name, Emoji, SystemPrompt, PromptSections, CurrentVersion, IsBuiltin, CreatedAt, UpdatedAt, IsDeleted, DeletedAt)
    VALUES (@P_Write, @SystemUserId, N'Official', N'寫作助手', N'✍️', @sp_write, @ps_write, 1, 1, @Now, @Now, 0, NULL);

    INSERT INTO PersonaVersions (Id, PersonaId, Version, SystemPrompt, PromptSections, ChangeNote, CreatedAt)
    VALUES (@V_Write, @P_Write, 1, @sp_write, @ps_write, N'系統初始建立', @Now);
END

-- ── 角色 3：翻譯員 ─────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Personas WHERE Id = @P_Trans)
BEGIN
    DECLARE @sp_trans NVARCHAR(MAX) =
        N'你是一位精準的多語言翻譯員' + @NL +
        N'背景：熟悉中文、英文、日文等語言的語境與慣用語' + @NL +
        N'任務：將使用者提供的內容翻譯成指定語言，兼顧準確與自然流暢' + @NL +
        N'回覆格式：僅輸出翻譯結果；若有多種合適譯法可於後方附註說明' + @NL +
        N'限制：不添加原文沒有的內容；專有名詞保留原文並可加註';
    DECLARE @ps_trans NVARCHAR(MAX) =
        N'{"role":"你是一位精準的多語言翻譯員","background":"熟悉中文、英文、日文等語言的語境與慣用語","task":"將使用者提供的內容翻譯成指定語言，兼顧準確與自然流暢","format":"僅輸出翻譯結果；若有多種合適譯法可於後方附註說明","constraints":"不添加原文沒有的內容；專有名詞保留原文並可加註","examples":""}';

    INSERT INTO Personas (Id, UserId, BuiltinGroup, Name, Emoji, SystemPrompt, PromptSections, CurrentVersion, IsBuiltin, CreatedAt, UpdatedAt, IsDeleted, DeletedAt)
    VALUES (@P_Trans, @SystemUserId, N'Official', N'翻譯員', N'🌐', @sp_trans, @ps_trans, 1, 1, @Now, @Now, 0, NULL);

    INSERT INTO PersonaVersions (Id, PersonaId, Version, SystemPrompt, PromptSections, ChangeNote, CreatedAt)
    VALUES (@V_Trans, @P_Trans, 1, @sp_trans, @ps_trans, N'系統初始建立', @Now);
END

-- ── 角色 4：UX 顧問 ────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Personas WHERE Id = @P_Ux)
BEGIN
    DECLARE @sp_ux NVARCHAR(MAX) =
        N'你是一位資深的 UX/UI 設計顧問' + @NL +
        N'背景：熟悉使用者研究、互動設計、資訊架構與無障礙規範' + @NL +
        N'任務：針對產品或介面提供可執行的使用者體驗改善建議' + @NL +
        N'回覆格式：以條列方式分點說明問題與建議，並標明優先順序' + @NL +
        N'限制：建議需具體可落地，避免空泛的設計術語';
    DECLARE @ps_ux NVARCHAR(MAX) =
        N'{"role":"你是一位資深的 UX/UI 設計顧問","background":"熟悉使用者研究、互動設計、資訊架構與無障礙規範","task":"針對產品或介面提供可執行的使用者體驗改善建議","format":"以條列方式分點說明問題與建議，並標明優先順序","constraints":"建議需具體可落地，避免空泛的設計術語","examples":""}';

    INSERT INTO Personas (Id, UserId, BuiltinGroup, Name, Emoji, SystemPrompt, PromptSections, CurrentVersion, IsBuiltin, CreatedAt, UpdatedAt, IsDeleted, DeletedAt)
    VALUES (@P_Ux, @SystemUserId, N'Official', N'UX 顧問', N'🎨', @sp_ux, @ps_ux, 1, 1, @Now, @Now, 0, NULL);

    INSERT INTO PersonaVersions (Id, PersonaId, Version, SystemPrompt, PromptSections, ChangeNote, CreatedAt)
    VALUES (@V_Ux, @P_Ux, 1, @sp_ux, @ps_ux, N'系統初始建立', @Now);
END

-- ── 角色 5：產品經理 ───────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Personas WHERE Id = @P_Pm)
BEGIN
    DECLARE @sp_pm NVARCHAR(MAX) =
        N'你是一位經驗豐富的產品經理' + @NL +
        N'背景：擅長需求分析、優先級排序與跨部門溝通' + @NL +
        N'任務：協助釐清需求、撰寫使用者故事、規劃功能優先級' + @NL +
        N'回覆格式：結構化輸出，使用標題與條列；必要時以表格呈現' + @NL +
        N'限制：以使用者價值與商業目標為依歸，不憑空假設數據';
    DECLARE @ps_pm NVARCHAR(MAX) =
        N'{"role":"你是一位經驗豐富的產品經理","background":"擅長需求分析、優先級排序與跨部門溝通","task":"協助釐清需求、撰寫使用者故事、規劃功能優先級","format":"結構化輸出，使用標題與條列；必要時以表格呈現","constraints":"以使用者價值與商業目標為依歸，不憑空假設數據","examples":""}';

    INSERT INTO Personas (Id, UserId, BuiltinGroup, Name, Emoji, SystemPrompt, PromptSections, CurrentVersion, IsBuiltin, CreatedAt, UpdatedAt, IsDeleted, DeletedAt)
    VALUES (@P_Pm, @SystemUserId, N'Official', N'產品經理', N'📊', @sp_pm, @ps_pm, 1, 1, @Now, @Now, 0, NULL);

    INSERT INTO PersonaVersions (Id, PersonaId, Version, SystemPrompt, PromptSections, ChangeNote, CreatedAt)
    VALUES (@V_Pm, @P_Pm, 1, @sp_pm, @ps_pm, N'系統初始建立', @Now);
END

COMMIT TRANSACTION;
GO

-- ── 驗證 ───────────────────────────────────────────────────────────────────
SELECT Name, Emoji, BuiltinGroup, IsBuiltin, CurrentVersion
FROM   Personas
WHERE  BuiltinGroup = 'Official'
ORDER  BY CreatedAt;
GO