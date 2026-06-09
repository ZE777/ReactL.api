# Discord 自然語言工具化（Agentic Function Calling）— 定案規格

> 狀態：**Phase 1–3 已實作並測試驗收（19/19 通過）+ 審查修正已套用** ｜ 建立 2026-06-05 ｜ 定案 2026-06-08 ｜ 驗收 2026-06-09（v1.0.0）
> 本次範圍：**僅 Discord**。LINE 未來能否支援見 §13。
> 目標：讓 Discord Bot 處理「依條件找人/找訊息再執行管理動作」的自然語言指令，
> 例如「把這 2 天內講過髒話的人禁言 30 秒」，而非只能對明確 @ 的單一對象操作。

---

## 1. 核心架構：多步 agent 迴圈（ReAct）

把現有單發 `CompleteWithToolsAsync` 之外，**新增**多步迴圈：

```
送出(system + user + tools)
 → AI 回 tool_calls → 後端執行 →（唯讀工具）結果以 role=tool 回灌 → 再送
 → 直到 AI 回純文字 / 命中 terminal 工具 / 達 maxSteps
```

功能拆兩半（兩半都通用，不綁髒話）：
- **找人**：AI 依「使用者描述的任意條件」讀訊息判斷誰符合。
- **動作**：沿用現有工具（禁言/踢/慢速/改身分組…）。

**依「必要參數是否足夠」自動決策**：參數齊（明確 @ 對象）→ 直接做；缺對象 → 先呼叫讀訊息工具找人再做。不寫死情境判斷。

## 2. 成本策略（重點）

成本主要來自「餵訊息給 AI」那一刻的 token，而非步數。控制組合：

1. **短路**：只有「缺對象、要 AI 找人」才進迴圈。日常指令（「禁言 @bob」）維持單發 1 次呼叫，成本不變。
2. **後端粗篩（可關鍵字化的條件才有）**：抓回的訊息先用便宜的關鍵字/正規化規則篩，只把可疑少數交 AI。純語意條件則靠 window 上限。
3. **便宜模型掃判**：`ScanModel` 預設 Groq 8B（快、夠用），可調 70B。Groq LPU 速度快，掃 ~120 則約 1~3 秒。
4. **設定值時間窗 + 則數上限**：見 §6，數學上封頂單次 token。
5. **每指令 token 預算護欄**：超過即停。

> 純語意條件無法粗篩，覆蓋範圍受「最近 N 則」window 限制（物理極限，透明告知「已分析最近 N 則」）。

## 3. 設定值驅動的範圍 + 超標拒絕 + 透明回報

- **時間窗**：未指定 → `DefaultScanWindowMinutes`；指定且 ≤ `MaxScanWindowMinutes` → 照做；**超過 → 不執行，回「已超出系統設定規範」並列出目前規則**（標 ❌ 在超標項）。

### 3.1 時間窗如何決定（AI 行為 + 後端護欄）
時間窗來自 AI 在 `fetch_recent_messages` 帶的 `since_minutes`，後端再套 `Default`/`Max`：

| 使用者講法 | AI 應帶的 `since_minutes` | 實際採用 |
|---|---|---|
| 沒提時間 / 只說「最近」「剛剛」（模糊） | **省略**（不帶） | `DefaultScanWindowMinutes` |
| 明確數字「最近 30 分鐘」「過去 2 小時」 | 帶對應分鐘數 | 該值（≤ Max） |
| 明確但超過上限「最近 10 天」 | 帶超過值 | **拒絕 + 列規則**（不執行） |

> ⚠️ **AI 易踩雷**：模糊的「最近」常被模型自己猜成某個小數字（如 10 分鐘）而**不省略**，導致預設值沒被套用。
> 因此 `fetch_recent_messages` 的 `since_minutes` 參數說明與 system prompt 都**明確要求**：沒有具體數字時務必「省略」此參數、不要自己猜。
> 對應實作：`DiscordToolService` 的工具 schema 描述、`DiscordWebhookService.BuildSystemPrompt`。

### 3.2 時間窗 vs 則數上限：誰先到算誰
實際掃描範圍 = `min(時間窗內的訊息, MaxScanMessages 則)`：

- 安靜頻道：時間窗先到（例如 7 天內只有 30 則 → 全收）。
- **忙碌頻道：`MaxScanMessages` 先到**（120 則可能只涵蓋幾小時）→ **設再長的時間窗也掃不滿**，實際覆蓋遠短於時間窗。
- 要真的覆蓋長時間，需調大 `MaxScanMessages`，但會**增加 token 用量、可能撞免費層 TPM**。
- 回報一律透明告知實際涵蓋（「已分析最近 N 則…」）。

- **回報由後端模板產生**（不靠 AI 記），一定帶「實際時間窗 + 動作 + 人數」。範例：
  - `✅ 已將最近 2 小時內講過髒話的 8 位成員禁言 30 秒。`
  - `✅ 已分析最近 120 則（約 1.5 小時內），將其中 8 位禁言 30 秒。`
  - `⚠️ 已超出系統設定規範，未執行。目前規範：• 掃描時間最多 24 小時（你要求 48 小時）❌ • 單次最多 120 則 • 單次最多影響 25 人。`
  - `✅ 已禁言 6 人；2 人因權限階級高於 Bot 略過。`

## 4. 確認機制

**觸發條件 = 危險動作（既有 RequiresConfirmation：ban/kick/改身分組/清訊息/慢速）OR 對象由 AI 解析（非人明確 @）。**
對照：
| 指令 | 確認？ |
|---|---|
| 禁言 @bob 30 秒 | 直接做（安全動作 + 明確指定） |
| 把講髒話的人禁言 30 秒 | **要確認（AI 自選對象）** |
| ban @bob | 要確認（危險動作） |
| 把搗亂的人 ban | 要確認（危險 + AI 自選） |

**可編輯名單**：確認時用 **User Select（type 5）多選清單，預設全選**（Discord 原生顯示人名/頭像）；管理員可**自由增減對象**（因清單已僅限管理員操作），再按〔執行〕→ 只對選中者動作。上限 = `min(25, MaxBatchTargets)`。
> 實作備註：原規劃為「只能減」，後因清單已由權限把關（見 §5），放寬為可自由增減，較符合實際使用。

**待確認暫存**：`IMemoryCache`，key=token，TTL=`PendingTtlMinutes`；存候選名單 + 目前勾選等。執行鈕 `custom_id="cf:batch:{token}"`、取消鈕 `cf:batchx:{token}`、多選變更 `selbatch:{token}`（名單塞不進 100 字 custom_id，故用 token）。多選更新採「**整體替換新集合**」避免與執行並行讀寫的 race。

## 5. 存取控制（appsettings 開關）

| 開關 | 預設 | 語意 |
|---|---|---|
| `AllowAnyoneInitiate` | **true** | 任何成員可發起「掃描+提名單」（唯讀無害）；false=需該動作權限才能發起 |
| `AllowAnyoneConfirm` | **false** | false=**僅具該動作權限者**可調整勾選/按執行（非管理員 → ephemeral 拒絕）；true=任何人可按（⚠️ 僅測試，正式請保持 false） |

- 「管理員」= **該動作對應的權限**（禁言→Moderate Members、ban→Ban Members…），用頻道層級權限判斷。
- `AllowAnyoneConfirm=false` 時，**調整勾選與按執行都鎖權限**（非管理員 → ephemeral 私訊拒絕、**不更動原訊息**，按鈕保留給管理員）。
- `AllowAnyoneInitiate=false` → 發起掃描前檢查 Moderate Members / Administrator（已實作於 `fetch_recent_messages` 入口）。
- **發起頻率節流分角色**：一般成員用 `InitiateCooldownSeconds`、管理員用較短的 `InitiateCooldownSecondsForAdmin`（僅針對「掃描」發起，一般聊天不受影響）。

## 6. appsettings：`DiscordAgentSettings`

```jsonc
"DiscordAgentSettings": {
  "Enabled": true,                            // 功能總開關；false 時 Discord 走原單輪
  "AllowAnyoneInitiate": true,
  "AllowAnyoneConfirm": false,
  "InitiateCooldownSeconds": 30,              // 【秒】一般成員發起冷卻
  "InitiateCooldownSecondsForAdmin": 10,      // 【秒】管理員發起冷卻（較短）
  "DefaultScanWindowMinutes": 10080,          // 【分】未指定時的掃描窗（預設 7 天）
  "MaxScanWindowMinutes": 10080,              // 【分】時間窗硬上限（超過→拒絕並列規則；需 ≥ 預設）
  "MaxScanMessages": 120,                     // 【則】單次最多分析則數（與時間窗誰先到算誰；壓在免費 TPM 下）
  "MaxBatchTargets": 25,                      // 【人】單次最多影響人數（程式內夾死 ≤25 對齊 Discord select）
  "MaxStepsPerCommand": 3,                    // agent 迴圈步數上限
  "MaxTokensPerCommand": 30000,               // 每指令 token 天花板（步間護欄）
  "PendingTtlMinutes": 5,                     // 【分】待確認暫存存活
  "ScanModel": ""                             // 留空＝用該 Bot 設定的模型；填便宜模型可省成本（小模型工具呼叫較不穩）
}
```
> 註：實測 Groq 免費層 TPM 太低、易限流，這個 agent 流程建議 Bot 用 **Mistral / Cerebras(GLM)** 等高額度供應商（見 §16 相關功能：模型推薦提示）。

## 7. 新工具

**`fetch_recent_messages`（唯讀，loop 內自動執行）**
- 參數：`since_minutes`（必填，受 Max 夾/拒絕）、`channel`（不填=目前頻道；MVP 僅當下頻道）。
- 後端：`GET /channels/{id}/messages` 以 `before` 分頁，到 cutoff 或 `MaxScanMessages`；排除 bot 訊息、空內容。
- 輸出精簡：`作者ID｜暱稱｜內容(截斷)`。
- **MESSAGE CONTENT intent 未開** → 內容大量為空 → 回明確提示要去後台開啟。

**`timeout_members`（批次，需確認）**
- 參數：`targets[]`、`amount`、`unit`、`reason`。
- 建確認前**過濾名單**：排除**指令發起人**、去重、上限夾 `min(25, MaxBatchTargets)`；bot 訊息已在 fetch 階段排除。
- **管理員 / 階級 ≥ Bot / 伺服器擁有者** 採「**執行時靠 Discord 403 略過並回報**」（未事前逐一查階級，省 API 呼叫）。
- 確認執行：逐一重用 `TimeoutMemberAsync`，逐筆回報成功/略過。

## 8. 錯誤處理（沿用共通化）
- 迴圈中 AI 失敗 → `UpstreamAiException`（友善訊息，含供應商名）→ 回覆。
- 唯讀工具執行失敗 → **直接終止並回報**（不讓 AI 反覆重試，成本可預測）。
- 詳見 `skills/AI串接錯誤處理指南.md`。

## 9. 審計留痕（方案 1+2）
- **Serilog** 一定記：發起人、指令文字、條件、時間窗、實際則數、候選名單、被剔除者、最終影響名單（逐人成功/略過）、按確認者、Guild/頻道、時間。
- **`ExternalMessages`** 寫一筆摘要 → 出現在現有「監控頁」供管理員查看。
- （未來需審計查詢頁再升級為專用 `AuditLogs` 表。）

## 10. 需新增/修改檔案
| 檔案 | 變更 |
|---|---|
| `DTOs/Ai/AiToolCalling.cs` | `AiToolCall` 加 `Id`；新增 agent 結果型別 |
| `Services/AI/IAiService.cs` + `OpenAiService.cs` | 解析 tool_call `id`；新增 `RunToolAgentAsync`（迴圈/組訊息/maxSteps/預算/token 累計） |
| `Services/Webhooks/IDiscordToolService.cs` + `DiscordToolService.cs` | `ToolSpec` 加 `ReadOnly`；新增 `fetch_recent_messages`、`timeout_members`；`ExecuteConfirmedAsync` 加 `cf:batch`；多選編輯 + 名單過濾 |
| `Services/Webhooks/DiscordQueryService.cs` | 抓頻道近 N 分鐘訊息（分頁） |
| `Services/Webhooks/DiscordWebhookService.cs` | `ProcessCommandAsync` 改走 `RunToolAgentAsync` + 提供 executor；接 terminal → 確認；component 處理 select 變更 + cf:batch |
| `Common/Settings/DiscordAgentSettings.cs` + `Program.cs` | 新設定 + `IMemoryCache`（確認 `AddMemoryCache`）+ 綁定 |

## 11. 平台前置（你手動）
- Discord 開發者後台 → Bot → Privileged Gateway Intents → 開 **MESSAGE CONTENT INTENT**（100 伺服器內免審核，直接開）。
- Bot 需 **Read Message History** + **Timeout Members** 權限。

## 12. 分階段（皆已實作並測試驗收）
- ✅ **Phase 1（地基）**：`AiToolCall.Id` + `RunToolAgentAsync` + `fetch_recent_messages` + `DiscordAgentSettings`/驗證/節流/總開關。
- ✅ **Phase 2（批次動作）**：`timeout_members` + 待確認暫存（IMemoryCache）+ **User Select 多選編輯** + `cf:batch` 執行 + 排除發起人/上限過濾 + 審計（Serilog + 監控頁摘要）。
- ✅ **Phase 3（推廣）**：批次機制泛化（PendingBatch + dispatch），已加 `remove_timeout_members`（批次解除禁言）作為第二個批次動作；新增更多批次動作只需加 ToolSpec + dispatch case。
- 備註：名單的「管理員/階級過濾」採**執行時靠 Discord 403 回報略過**（非事前逐一查階級），避免大量 member 查詢；bot 訊息在 fetch 階段已排除。

## 13. 未來 LINE 群組？（受限）
- **動作（禁言/踢人）**：LINE Messaging API **不開放群組成員管理**，bot 無法踢/禁言/限制成員 → **做不到**。
- **找人**：LINE **無歷史訊息 API**，須自行**即時側錄**群組訊息；取群組成員 id 需認證/Premium 帳號。
- 結論：未來 LINE 版本只能「**掃描 + 報告/點名提醒**」，無法「掃描 + 處置」。
- 核心 agent 迴圈與「找人」邏輯可重用，未來接 LINE 僅換資料源 + 動作改為通知。

## 14. 驗收情境
1. 明確 @ 單一 → 直接執行。 2. 「掃近 2 天講髒話者禁言 30 秒」→ 抓→判→多選確認→執行→回報。 3. 無人違規 → 「找不到符合對象」。 4. 未開 intent → 提示開啟。 5. 名單含管理員/高階級 → 過濾或回報略過。 6. 超 Max 時間/則數/人數 → 列規則拒絕。 7. 迴圈中 429 → 友善限流訊息。 8. 待確認逾時 → 「已過期，請重下」。 9. 非權限者按按鈕（Confirm=false）→ ephemeral 拒絕。 10. 取消 → 不執行。

## 15. 相容性
- 保留現有 `CompleteWithToolsAsync`；新增 `RunToolAgentAsync`。
- 只有 Discord webhook 改走新方法；LINE、後台聊天、前台聊天不動。`Enabled=false` 時 Discord 亦走原單輪（system prompt 也會切成單輪版，不提掃描/批次工具，避免誤用刪訊息等動作代替）。

## 16. 同批一起實作的相關功能
- **模型推薦提示（Discord 限定）**：`AiModelConfig.RecommendedForTools` 旗標 → `/ai/providers` 帶到後台；Bot 新增/編輯的模型選單對推薦模型顯示「推薦」標籤、選到非推薦顯示警告（LINE 不顯示，因無 function-calling）。目前推薦：Mistral Small/Large/7B、ZAI GLM 4.7（實測工具呼叫穩且免費額度足；Groq 免費 TPM 太低易限流）。
- **內建（Official）角色限 Admin**：`PersonaService` 依 `isAdmin` 過濾——Admin 可檢視/管理 Official + 自己的；自建帳號（User）僅本人自訂，看不到/存取不到 Official。前台公開聊天不受影響（`GetPublicPersonasAsync` 不變）。

## 17. 審查修正（已套用）與待辦 follow-up
**已套用（code review 後）**：
- `ScanModel` 真正生效（留空＝Bot 模型，不回歸）。
- `AllowAnyoneInitiate` 真正生效（false → 發起需權限）。
- 待確認名單並行 race 修正（整體替換新集合）。
- 取消鈕 `cf:batchx` 補權限重檢。
- 名單上限程式內夾死 ≤25。
- `Enabled=false` 排除 agent-only 工具 + 切換單輪版 system prompt。
- 非管理員按確認/取消/多選 → ephemeral 拒絕且不破壞按鈕。

**待辦（已知、尚未處理）**：
- 單一危險動作（ban/kick…）的 `custom_id` 未 token 化 → 有權限者理論上可自構 `cf:ban:<任意ID>` 繞過確認。建議改 token+暫存。
- **Persona API 漏洞**：BotBinding / Conversation 設定 `PersonaId` 未驗證可存取性 → 一般使用者可用自製 API 把 Bot/對話綁到 Official 角色（UI 已過濾，僅 API 層可繞）。
- 節流可繞過（多帳號/多 Bot、單機 cache）、Ed25519 缺公鑰時放行、`DescribeDuration` 混合時長顯示。
