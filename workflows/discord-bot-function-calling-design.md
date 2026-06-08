# Discord Bot AI Function Calling 設計文件

> 讓 Discord 使用者在 `/chat` 用**自然語言**（例如「幫我禁言 @AA 300 秒」「把 @BB 移到 #語音二」）
> 觸發 Bot 對伺服器執行管理動作。AI 負責「聽懂語意、決定呼叫哪個工具」，
> **後端負責把工具呼叫映射到真正的 Discord API 並驗證執行**。

---

## 1. 目的與背景

現況：Discord Bot 走 **HTTP Interactions（webhook）**，目前只有 `/chat`，僅將文字丟給 AI 並回傳文字，AI **無法**對伺服器做任何操作。

目標：在 `/chat` 加上 **AI function calling（工具呼叫）**，讓 AI 從**白名單工具**中選擇並帶參數，後端驗證後呼叫 Discord API 執行（禁言、移動、查詢…）。

關鍵原則：**AI 只決定「做什麼」（工具 + 參數），後端決定「怎麼做」（API 呼叫 + 驗證 + 防護）。AI 永遠碰不到 Discord API 本身**，避免 prompt injection 造成越權操作。

---

## 2. 功能流程（end-to-end）

```
1. 使用者在頻道打：/chat 幫我禁言 @AA 300秒、把 @BB 移到 #語音二
2. Discord 把 interaction POST 到後端 webhook
   - string 參數值內的提及會變成 <@AA的ID>、<#頻道ID>
   - data.resolved 附上這些 ID 對應的 user/channel/role 物件
   - member.permissions 帶有「下指令者」的權限位元
3. 後端先 defer（回「思考中…」），把使用者文字 + 白名單工具清單丟給 AI
4. AI 回傳工具呼叫（非純文字）：
   timeout_member(target="<@AA的ID>", amount=300, unit="second")
   move_member(target="<@BB的ID>", channel="<#語音二ID>")
5. 後端逐一執行：
   a. 從 <@...>/<#...> 解析 ID，並用 resolved 驗證存在
   b. 權限檢查：下指令者本身有對應權限嗎？(member.permissions)
   c. 參數驗證：數值/長度範圍、目標型別（見 §6）
   d. 需二次確認的動作 → 先回覆「確認」按鈕，待按下才執行（見 §7）
   e. 透過 Discord.Net 呼叫 Discord API 執行
   f. 收集每個動作的結果（成功/失敗原因）
6. 後端 PATCH followup，把「思考中…」換成結果，並寫入監控紀錄 + Token 統計
   ※ 若 AI 判斷只是聊天（無 tool_calls）→ 照舊回 AI 文字
```

---

## 3. 架構與實作設計

### 3.1 擴充 Discord interaction 解析
`DTOs/Requests/Webhooks/DiscordInteractionPayload.cs` 需新增：
- `DiscordInteractionData.Resolved`：含 `users` / `members` / `channels` / `roles`（@提及的真實 ID 來源）
- `DiscordMember.Permissions`（string 位元）：判斷**下指令者**有無對應權限

> interaction 是 Discord **主動推**來的，資料已包在 POST body 內，**解析即可、不需另外查 API**。

### 3.2 AI 服務加「帶工具」呼叫
`IAiService` 新增方法（不動現有 `CompleteWithUsageAsync`）：
```
Task<AiToolResult> CompleteWithToolsAsync(systemPrompt, userPrompt, tools, ownerUserId, ct)
```
- request body 加 `tools` + `tool_choice="auto"`（OpenAI 相容，Groq/SambaNova 支援）
- 解析回應 `choices[0].message.tool_calls`
- 回傳 `AiToolResult { ToolCalls[] 或 TextReply }`
- ⚠️ 所選模型必須**支援 tool calling**（建議 Groq Llama 3.3 70B；8B 小模型較不穩）
- 此路徑的回應上限獨立設定 **`AiSettings.ToolCallMaxTokens`（預設 2048）**，與一般聊天的 `MaxTokens` 分開：
  工具路徑常用 TPM 較低的小模型、且 Discord 訊息會截斷，較小值可避免「請求超過模型每分鐘 token 上限（TPM）」而被回 413

### 3.3 工具執行器 + Discord 管理服務
- **手刻 HTTP** 呼叫 Discord REST API（沿用 `PatchFollowupAsync` 的範式：`HttpClient` + `Authorization: Bot {token}` + `JsonSerializer`）
- 新增 `IDiscordModerationService` / `DiscordModerationService`：封裝各動作（timeout、move、kick…），每個動作對應一個 REST 端點
- 工具 dispatcher：依 AI 回傳的工具名稱分派到對應方法

### 3.4 編排（改 `DiscordWebhookService`）
在現有「取文字 → 呼叫 AI → 回覆」中插入工具分支：有 tool_calls → 驗證 → （需確認則先確認）→ 執行 → 組回覆；無 → 照舊回文字。

### 3.5 預計新增/修改檔案
- 新增：`IDiscordModerationService` + 實作、`AiToolResult` DTO、工具定義（schema 常數）、HELP 工具
- 修改：`DiscordInteractionPayload`、`IAiService` + `OpenAiService`、`DiscordWebhookService`、`Program.cs`（DI）
- **無 DB 結構異動**

---

## 4. 全域決策

| 項目 | 決策 |
|------|------|
| Discord API 實作方式 | **手刻 HTTP**（HttpClient + JsonSerializer，沿用現有範式）。見下方「決策紀錄」 |
| 開放範圍 | **白名單制**，只開放下表確認的功能 |
| 二次確認策略 | **逐功能依風險**：低風險免確認；中高風險需確認 |
| AI 邊界 | AI 只能選白名單工具，無法指定任意 API |
| 共通安全控管 | ① 下指令者權限檢查 ② Bot 階級錯誤處理（403 友善回覆）③ 參數範圍驗證 ④ 完整 log |

### 決策紀錄：為何採「手刻 HTTP」而非 Discord.Net（ADR）

> 規劃初期曾選定引入 Discord.Net 套件，實作前評估後**改為手刻 HTTP**，理由記錄如下供未來參考。

**問題本質：本後端是「一個程式、多隻 Bot」的多租戶、無狀態 webhook 架構。**
- 多租戶：一個後端同時代管多隻 Discord Bot，每隻各自一組加密 Token（存於各 BotBinding），**沒有「唯一的 Bot Token」**。
- 無狀態：走 HTTP Interactions，Discord 推來請求才處理，**不維持任何連線**。

**Discord.Net 的衝突點：** 它的 client **在 login 當下就綁死一個 Token = 一個 Bot 身分**，一個 client 無法分身服務不同 Bot。在多 Token 下只能：(a) 每次請求臨時 login（多開銷、且 login 還會多打幾個 round-trip：login→getGuild→getUser→action），或 (b) 自建「Token→client」連線池（記憶體/生命週期/執行緒安全都要自管）。

**手刻 HTTP 的優勢：** Discord REST API 以**每次請求帶 `Authorization: Bot {token}` header** 驗證，**沒有 login／連線概念**。我們在 interaction payload 中**已握有 guildId 與目標 userId**，故每個動作只需**一次** REST 呼叫（例如 timeout = 一次 `PATCH member`），不必先抓物件。且與現有 `PatchFollowupAsync`、指令註冊 `RegisterChatCommandAsync` **同一範式**，零新依賴。

**取捨對照：**
| | 手刻 HTTP（採用） | Discord.Net（不採用） |
|---|---|---|
| 身分綁定時機 | 每次呼叫（header 帶 Token） | 登入時（1 client = 1 Token） |
| 每動作 API 次數 | 1 次（ID 已在手） | ~4 次（含 login） |
| 多 Token | 一個 HttpClient 服務全部，無登入 | 需多 client / 連線池 / 每次重登 |
| 與現有程式一致 | ✅ 完全一致 | ❌ 另一套風格 + 重依賴 |

**結論：** Discord.Net 的優勢（事件監聽、狀態快取、強型別）在「無狀態 webhook + REST 動作」幾乎用不到，反而帶來多 Token 的管理負擔。故採手刻 HTTP。唯一代價是自寫 endpoint/body，但端點簡單且功能數有限，可接受。

---

## 5. 完整功能總表（定案）

**共 23 個工具開放給 AI（含 HELP-01），其中 6 個需二次確認。**
（原 26 扣除決議不實作的 MSG-02/04/05；需二次確認者：MOD-09、MOD-10、ROLE-01、ROLE-02、MSG-03、CH-01。）

> **實作狀態：✅ 上述 23 個工具已全部實作，並於 2026-06-05 完成完整測試（30 項測試案例全數通過）。**
> 涵蓋：所有功能動作、二次確認（按鈕→確認→執行）、特權身分組防護（ROLE-01b 正確擋下）、
> 取消流程、以及邊界（無權限、階級不足、超範圍、DM）。
> 使用者面的操作說明（每個功能的自然語言範例 + **所需 Bot 權限對照表** + 二次確認流程）見
> 前端 repo：`docs/部署指南/Discord-Bot-AI管理功能使用說明.md`。
圖示：給AI ✅推薦 / ⚠️謹慎　｜　二次確認 否 / 是 / 必須

### MOD — 成員管理
| 代號 | 功能 | 權限 | 給AI | 二次確認 |
|------|------|------|:--:|:--:|
| MOD-01 | 禁言 timeout | MODERATE_MEMBERS | ✅ | 否 |
| MOD-02 | 解除禁言 | MODERATE_MEMBERS | ✅ | 否 |
| MOD-03 | 移動語音頻道 | MOVE_MEMBERS | ✅ | 否 |
| MOD-04 | 踢出語音(斷線) | MOVE_MEMBERS | ✅ | 否 |
| MOD-05 | 語音禁麥 | MUTE_MEMBERS | ✅ | 否 |
| MOD-06 | 解除禁麥 | MUTE_MEMBERS | ✅ | 否 |
| MOD-07 | 語音禁聽 deafen | DEAFEN_MEMBERS | ✅ | 否 |
| MOD-08 | 改成員暱稱 | MANAGE_NICKNAMES | ✅ | 否 |
| MOD-09 | 踢出成員 kick | KICK_MEMBERS | ✅ | **必須** |
| MOD-10 | 封鎖 ban | BAN_MEMBERS | ✅ | **必須** |
| MOD-11 | 解除封鎖 unban | BAN_MEMBERS | ✅ | 否 |

### ROLE — 身分組
| 代號 | 功能 | 權限 | 給AI | 二次確認 |
|------|------|------|:--:|:--:|
| ROLE-01 | 賦予成員身分組（限非特權） | MANAGE_ROLES | ✅ | **是** |
| ROLE-02 | 移除成員身分組（限非特權） | MANAGE_ROLES | ✅ | **是** |

### MSG — 訊息
| 代號 | 功能 | 權限 | 給AI | 二次確認 |
|------|------|------|:--:|:--:|
| MSG-01 | 發送訊息 | SEND_MESSAGES | ✅ | 否 |
| MSG-03 | 批次刪訊息 purge | MANAGE_MESSAGES | ✅ | **必須** |
| ~~MSG-02~~ | ~~刪除單則訊息~~ | — | ❌ 不實作 | — |
| ~~MSG-04~~ | ~~釘選/取消釘選~~ | — | ❌ 不實作 | — |
| ~~MSG-05~~ | ~~加表情反應~~ | — | ❌ 不實作 | — |

> **MSG-02 / MSG-04 / MSG-05 決議「不實作」：** 這三個需指定「特定訊息」，`/chat` 自然語言拿不到訊息 ID；
> 而使用者本來就能用 **Discord 原生右鍵**自行刪除/釘選/加表情，無須由 Bot 重做。故移除（連帶取消原 S7 階段）。
> MSG-03（批次刪最近 N 則）不需指定單一訊息，仍走 `/chat`，保留。

### CH — 頻道
| 代號 | 功能 | 權限 | 給AI | 二次確認 |
|------|------|------|:--:|:--:|
| CH-01 | 設定慢速模式 slowmode | MANAGE_CHANNELS | ✅ | **是** |

### SRV — 伺服器
| 代號 | 功能 | 權限 | 給AI | 二次確認 |
|------|------|------|:--:|:--:|
| SRV-01 | 查看審核日誌 audit log | VIEW_AUDIT_LOG | ✅ | 否 |

### QRY — 查詢（唯讀）
| 代號 | 功能 | 給AI | 二次確認 |
|------|------|:--:|:--:|
| QRY-01 | 查成員資訊 | ✅ | 否 |
| QRY-02 | 查禁言/封鎖狀態 | ✅ | 否 |
| QRY-03 | 列出頻道 | ✅ | 否 |
| QRY-04 | 列出/搜尋成員 | ✅ | 否 |
| QRY-05 | 查身分組清單 | ✅ | 否 |

### HELP — 使用說明（必備「口子」）
| 代號 | 功能 | 給AI | 二次確認 | 說明 |
|------|------|:--:|:--:|------|
| HELP-01 | 查詢可用功能 / 使用說明 | ✅ | 否 | 語意如「你能幫我做什麼」「功能清單」「使用說明」「help」時，AI 呼叫此工具，後端回傳「目前開放的功能清單與用法」。唯讀、不碰 Discord 操作、免權限。**清單應由開放工具動態產生，新增工具時自動同步，避免說明與實作脫節。** |

### 排除清單（討論後決定**不交給 AI**，列出以保留完整紀錄）
| 代號 | 功能 | 權限 | 不納入原因 |
|------|------|------|------|
| ROLE-03 | 建立身分組 | MANAGE_ROLES | 結構性變更，高破壞性 |
| ROLE-04 | 編輯身分組(權限/顏色) | MANAGE_ROLES | 可被用來提權，高風險 |
| ROLE-05 | 刪除身分組 | MANAGE_ROLES | 不可逆破壞 |
| CH-02 | 管理討論串(封存/關閉) | MANAGE_THREADS | 非必要，暫不開放 |
| CH-03 | 建立頻道 | MANAGE_CHANNELS | 結構性變更 |
| CH-04 | 編輯頻道(名稱/主題) | MANAGE_CHANNELS | 高破壞性 |
| CH-05 | 刪除頻道 | MANAGE_CHANNELS | 不可逆破壞 |
| CH-06 | 編輯頻道權限 | MANAGE_ROLES | 可被用來提權，高風險 |
| SRV-02 | 改伺服器設定 | MANAGE_GUILD | 影響全伺服器 |
| SRV-03 | 管理表情/貼圖 | MANAGE_GUILD_EXPRESSIONS | 非必要 |
| SRV-04 | 管理 Webhook | MANAGE_WEBHOOKS | 可被用來外洩/偽造訊息 |
| SRV-05 | 管理活動 events | MANAGE_EVENTS | 非必要，暫不開放 |
| ADM-00 | ADMINISTRATOR 管理者 | ADMINISTRATOR | 含一切權限，Token 外洩=整伺服器淪陷，**永不開放** |

> **全功能盤點總計：** 討論涵蓋 39 個能力 → **納入 26 個**（含 HELP-01）、**排除 13 個**（上表，其中 ADM-00 為永久禁用）。
> （MOD ×11、ROLE ×2、MSG ×5、CH ×1、SRV ×1、QRY ×5、HELP ×1 納入；ROLE ×3、CH ×5、SRV ×4、ADM ×1 排除。）

---

## 6. 參數範圍規範

### 需「數值 / 長度範圍」
| 代號 | 參數 | Discord 硬限制 | 採用範圍 |
|------|------|------|------|
| MOD-01 | 禁言時長 | 最長 28 天 | 10 秒 ~ 28 天 |
| MOD-08 | 暱稱 | 1~32 字 | 1~32 字 |
| MOD-10 | 封鎖時刪除近期訊息天數 | 0~7 天 | 預設 0，上限 7 天 |
| MSG-01 | 訊息內容 | 1~2000 字 | ≤2000，超過截斷 |
| MSG-03 | 批次刪則數 | 2~100、僅 14 天內 | 2~100 |
| CH-01 | 慢速秒數 | 0~21600 秒（6h） | 0~21600 |
| SRV-01 | 審核日誌回傳筆數 | 1~100 | 預設 10、上限 50 |
| QRY-04 | 列出/搜尋成員筆數 | 分頁 | 上限 25~50 |
| QRY-03/05 | 列出頻道/身分組筆數 | — | 設合理上限 |

### 需「目標型別 / 白名單」驗證
| 代號 | 驗證內容 |
|------|------|
| MOD-03 | 目標必須是**存在的語音頻道**（非文字頻道） |
| ROLE-01 / ROLE-02 | 身分組**存在** + **非特權**（不得帶管理/封鎖/踢人等危險權限）+ **低於 Bot 階級** |
| MOD-09 / MOD-10 | 選填「原因」字串上限 512 字（寫入審核日誌） |
| MSG-05 | emoji 必須有效（Unicode 或本伺服器自訂） |
| 所有帶 target 者 | 目標 user/channel/role 必須在**本伺服器**（用 resolved 驗證） |

### 共通底線
**超出範圍 → 不丟給 Discord，後端先擋下並回友善錯誤**（例如「禁言最長 28 天」）。
建議 AI 輸出 `{amount, unit}`，**換算與驗證一律在後端**，避免 LLM 單位換算出錯。

---

## 7. 二次確認機制（中高風險動作）

適用：MOD-09、MOD-10、ROLE-01、ROLE-02、MSG-02、MSG-03、CH-01。

設計：
- 後端先以 Discord 訊息 + **按鈕元件**（components）回覆：「⚠️ 即將對 @AA 執行『封鎖』，確認？ [確認] [取消]」
- 使用者按下按鈕 → Discord 送來 MESSAGE_COMPONENT interaction → 後端再執行
- 確認資料（要對誰做什麼）以短期狀態保存（custom_id 帶必要參數或暫存）
- 逾時未確認則作廢

---

## 8. 安全控管總則
1. **AI 邊界**：只能選白名單工具，碰不到任意 API。
2. **下指令者權限**：讀 `member.permissions`，無對應權限即拒絕。
3. **Bot 階級**：執行若回 403 → 友善回覆「階級不足/缺權限」。
4. **參數驗證**：數值/長度/型別/存在性，全部後端把關。
5. **特權身分組保護**：ROLE 操作排除帶危險權限的身分組。
6. **完整 log**：每次工具呼叫記錄「誰、對誰、做什麼、結果」。
7. **絕不開放**：ADMINISTRATOR 及破壞性結構操作。

---

## 9. 實作階段順序

| 階段 | 內容 | 代號 |
|------|------|------|
| **S1** | 地基（interaction 解析、AI tool-calling、Discord REST 呼叫封裝、編排、安全控管）+ 首批工具 + HELP-01 | MOD-01、MOD-02、HELP-01 |
| **S2** | 其餘免確認成員動作 | MOD-03~08、MOD-11 |
| **S3** | 查詢類（唯讀，驗證多工具） | QRY-01~05、SRV-01 |
| **S4** | **二次確認機制**（按鈕互動）+ 首批需確認工具 | MOD-09、MOD-10 |
| **S5** | 其餘需確認工具 | ROLE-01/02、MSG-02/03、CH-01 |
| **S6** | 其餘免確認訊息工具 | MSG-01（發送訊息） |

> （原規劃的 S7「訊息右鍵選單」已取消；MSG-02/04/05 決議不實作，使用者用 Discord 原生右鍵處理。）

> HELP-01 於 S1 一併建立，並設計成**動態列出目前已註冊的工具**，後續階段新增工具時自動同步。

---

## 10. 風險與注意事項
- **模型 tool calling 支援**：綁定模型須支援；不支援需改「結構化輸出 + 後端解析」備援。
- **AI 誤判**：中高風險動作以二次確認 + 完整 log 緩解。
- **單位換算**：交由後端，勿信任 AI 自算秒數。
- **名稱解析**：依賴 @提及 / #頻道（resolved 提供 ID）；純文字名稱不保證可解析。
- **延遲**：先 defer，interaction token 有 15 分鐘，足夠 AI + 執行。

---

## 11. 測試與調整紀錄（2026-06-05）

實測 23 功能（30 項測試案例）期間發現並修正的問題，記錄供日後參考：

| 問題 | 現象 | 修正 |
|------|------|------|
| 小模型布林值出錯 | 8b 模型把 `muted`/`deafened` 輸出成字串，Groq schema 驗證回 400 | 這兩個參數改成**字串列舉 `["true","false"]`**，後端 `GetBool` 解析；弱模型也能用 |
| 「思考中」卡死 | Groq 每日額度 429 的 `Retry-After` 達 25~47 分，重試傻等，超過 Discord 15 分鐘 token 時限，followup 永遠送不出 | 重試等待**封頂 10 秒**（`MaxRetryDelay`），過長的 Retry-After 直接快速失敗回報 |
| 錯誤訊息不明確 | 任何 AI 錯誤都回「抱歉，AI 服務暫時無法回應」，看不出是額度上限還是格式錯 | Webhook 單獨 catch `UpstreamAiException` 顯示其友善訊息；並補上 400 的專屬訊息 |
| 工具路徑回應過長/TPM | 一般 `MaxTokens`(8192) 在低 TPM 小模型上請求過大被 413 | 新增 **`AiSettings.ToolCallMaxTokens`（預設 2048）**，與一般聊天分開 |

**測試心得 / 注意：**
- **模型選擇**：8b 小模型雖支援工具但格式不穩、TPM/TPD 低，易 400/429；建議用 **Cerebras `gpt-oss-120b`** 或 **SambaNova / Groq 70B** 等較穩、額度較寬的模型。
- **權限與階級（最常踩）**：
  - 暱稱用 **「管理暱稱」** 非「更改暱稱」（後者只能改自己）。
  - **語音類動作（禁麥/禁聽/移動/中斷）不檢查身分組階級**；但**禁言/改暱稱/踢人/封鎖/身分組會檢查階級** → Bot 身分組必須**高於**對象，否則回 403（code 50013）。擁有者永遠無法被動作。
- 每次 `/chat` 會送出全部工具 schema（約 3000+ token），是額度消耗主因；必要時可精簡工具描述以省 token。