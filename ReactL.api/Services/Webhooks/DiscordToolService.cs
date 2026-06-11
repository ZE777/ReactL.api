using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using ReactL.api.Common.Settings;
using ReactL.api.DTOs.Ai;
using ReactL.api.Models.BotBindings;
using ReactL.api.Services.BotBindings;
using System.Text;
using System.Text.Json;

namespace ReactL.api.Services.Webhooks
{
    /// <summary>
    /// Discord function-calling 工具服務（S1：MOD-01 禁言、MOD-02 解除禁言、HELP-01 功能說明）。
    /// 每個工具：① 定義給 AI 的 schema ② 驗證下指令者權限與參數 ③ 呼叫管理服務執行。
    /// 新增工具時於此擴充，HELP-01 會自動列出所有已註冊工具，說明與實作不脫節。
    /// </summary>
    public class DiscordToolService : IDiscordToolService
    {
        private readonly IDiscordModerationService _moderation;
        private readonly IDiscordQueryService _query;
        private readonly ILogger<DiscordToolService> _logger;
        private readonly IMemoryCache _cache;
        private readonly DiscordAgentSettings _agent;
        private readonly IBotTrustService _trust;

        // Discord 權限位元
        private const ulong PERM_ADMINISTRATOR = 1UL << 3;
        private const ulong PERM_BAN_MEMBERS = 1UL << 2;
        private const ulong PERM_VIEW_AUDIT_LOG = 1UL << 7;
        private const ulong PERM_MUTE_MEMBERS = 1UL << 22;
        private const ulong PERM_DEAFEN_MEMBERS = 1UL << 23;
        private const ulong PERM_MOVE_MEMBERS = 1UL << 24;
        private const ulong PERM_MANAGE_NICKNAMES = 1UL << 27;
        private const ulong PERM_MODERATE_MEMBERS = 1UL << 40;
        private const ulong PERM_SEND_MESSAGES = 1UL << 11;
        private const ulong PERM_KICK_MEMBERS = 1UL << 1;
        private const ulong PERM_MANAGE_CHANNELS = 1UL << 4;
        private const ulong PERM_MANAGE_MESSAGES = 1UL << 13;
        private const ulong PERM_MANAGE_ROLES = 1UL << 28;

        // 賦予身分組時，禁止賦予帶有以下任一「特權」的身分組（防提權）
        private const ulong PRIVILEGED_ROLE_MASK =
            PERM_ADMINISTRATOR | PERM_KICK_MEMBERS | PERM_BAN_MEMBERS | PERM_MANAGE_ROLES |
            PERM_MANAGE_CHANNELS | PERM_MODERATE_MEMBERS | (1UL << 5) /*MANAGE_GUILD*/;

        private const int MESSAGE_MAX_LENGTH = 2000;
        private const int PURGE_MAX = 100;
        private const int SLOWMODE_MAX = 21600;

        // 禁言時長範圍：10 秒 ~ 28 天（Discord 上限）
        private const int TIMEOUT_MIN_SECONDS = 10;
        private const int TIMEOUT_MAX_SECONDS = 28 * 24 * 60 * 60;
        private const int NICKNAME_MAX_LENGTH = 32;
        private const int QUERY_LIST_LIMIT = 50;      // 列表類查詢回傳上限
        private const int QUERY_SEARCH_LIMIT = 25;    // 成員搜尋回傳上限
        private const int AUDIT_LOG_LIMIT = 10;       // 審核日誌回傳筆數

        public DiscordToolService(
            IDiscordModerationService moderation, IDiscordQueryService query, ILogger<DiscordToolService> logger,
            IMemoryCache cache, IOptions<DiscordAgentSettings> agentOptions, IBotTrustService trust)
        {
            _moderation = moderation;
            _query = query;
            _logger = logger;
            _cache = cache;
            _agent = agentOptions.Value;
            _trust = trust;
        }

        /// <summary>工具中繼資料：名稱、說明、AI schema、所需權限、是否需二次確認</summary>
        private record ToolSpec(string Name, string Description, object Parameters, ulong RequiredPermission, bool RequiresConfirmation = false);

        private static readonly List<ToolSpec> Specs = new()
        {
            new ToolSpec(
                "timeout_member",
                "禁言（timeout）一位伺服器成員一段時間。當使用者要求「禁言/關/ban幾分鐘」某人時呼叫。",
                new
                {
                    type = "object",
                    properties = new
                    {
                        target = new { type = "string", description = "要禁言的成員，使用 Discord 提及格式 <@使用者ID>" },
                        amount = new { type = "integer", description = "禁言時長的數量（正整數）" },
                        unit = new { type = "string", @enum = new[] { "second", "minute", "hour", "day" }, description = "時長單位" }
                    },
                    required = new[] { "target", "amount", "unit" }
                },
                PERM_MODERATE_MEMBERS),

            new ToolSpec(
                "remove_timeout",
                "解除一位成員的禁言。當使用者要求「解禁/取消禁言」某人時呼叫。",
                new
                {
                    type = "object",
                    properties = new
                    {
                        target = new { type = "string", description = "要解除禁言的成員，使用 Discord 提及格式 <@使用者ID>" }
                    },
                    required = new[] { "target" }
                },
                PERM_MODERATE_MEMBERS),

            new ToolSpec(
                "move_member",
                "把一位成員移動到指定的語音頻道。當使用者要求「把某人移到某語音頻道」時呼叫。",
                new
                {
                    type = "object",
                    properties = new
                    {
                        target = new { type = "string", description = "要移動的成員，使用 <@使用者ID>" },
                        channel = new { type = "string", description = "目標語音頻道，使用 <#頻道ID> 格式" }
                    },
                    required = new[] { "target", "channel" }
                },
                PERM_MOVE_MEMBERS),

            new ToolSpec(
                "disconnect_voice",
                "將一位成員從語音頻道踢出（中斷其語音連線）。當使用者要求「把某人踢出語音/斷線」時呼叫。",
                new
                {
                    type = "object",
                    properties = new { target = new { type = "string", description = "要中斷語音的成員 <@使用者ID>" } },
                    required = new[] { "target" }
                },
                PERM_MOVE_MEMBERS),

            new ToolSpec(
                "voice_mute",
                "語音禁麥或解除禁麥一位成員。muted=true 為禁麥、false 為解除。當使用者要求「禁麥/解除禁麥」某人時呼叫。",
                new
                {
                    type = "object",
                    properties = new
                    {
                        target = new { type = "string", description = "對象成員 <@使用者ID>" },
                        muted = new { type = "string", @enum = new[] { "true", "false" }, description = "true=禁麥；false=解除禁麥" }
                    },
                    required = new[] { "target", "muted" }
                },
                PERM_MUTE_MEMBERS),

            new ToolSpec(
                "voice_deafen",
                "語音禁聽或解除禁聽一位成員。deafened=true 為禁聽、false 為解除。",
                new
                {
                    type = "object",
                    properties = new
                    {
                        target = new { type = "string", description = "對象成員 <@使用者ID>" },
                        deafened = new { type = "string", @enum = new[] { "true", "false" }, description = "true=禁聽；false=解除禁聽" }
                    },
                    required = new[] { "target", "deafened" }
                },
                PERM_DEAFEN_MEMBERS),

            new ToolSpec(
                "set_nickname",
                "變更一位成員的伺服器暱稱。當使用者要求「把某人暱稱改成XXX」時呼叫。",
                new
                {
                    type = "object",
                    properties = new
                    {
                        target = new { type = "string", description = "對象成員 <@使用者ID>" },
                        nickname = new { type = "string", description = "新暱稱（1~32 字）；留空字串表示清除暱稱" }
                    },
                    required = new[] { "target", "nickname" }
                },
                PERM_MANAGE_NICKNAMES),

            new ToolSpec(
                "unban_member",
                "解除對某位使用者的封鎖。當使用者要求「解除封鎖/解封」某人時呼叫。對象通常以使用者 ID 提供。",
                new
                {
                    type = "object",
                    properties = new { target = new { type = "string", description = "要解除封鎖的使用者，使用 <@使用者ID> 或純數字使用者 ID" } },
                    required = new[] { "target" }
                },
                PERM_BAN_MEMBERS),

            new ToolSpec(
                "get_member_info",
                "查詢一位成員的資訊（暱稱、加入時間、身分組數、是否禁言）。當使用者問「某人的資料/資訊」時呼叫。",
                new
                {
                    type = "object",
                    properties = new { target = new { type = "string", description = "對象成員 <@使用者ID>" } },
                    required = new[] { "target" }
                },
                0),

            new ToolSpec(
                "get_member_status",
                "查詢一位成員目前的禁言 / 封鎖狀態。當使用者問「某人有沒有被禁言/封鎖」時呼叫。",
                new
                {
                    type = "object",
                    properties = new { target = new { type = "string", description = "對象成員 <@使用者ID>" } },
                    required = new[] { "target" }
                },
                0),

            new ToolSpec(
                "list_channels",
                "列出伺服器的頻道。當使用者問「有哪些頻道」時呼叫。",
                new { type = "object", properties = new { } },
                0),

            new ToolSpec(
                "search_members",
                "依名稱關鍵字搜尋伺服器成員。當使用者問「有沒有叫XXX的人/找XXX」時呼叫。",
                new
                {
                    type = "object",
                    properties = new { query = new { type = "string", description = "成員名稱關鍵字" } },
                    required = new[] { "query" }
                },
                0),

            new ToolSpec(
                "list_roles",
                "列出伺服器的身分組。當使用者問「有哪些身分組/角色」時呼叫。",
                new { type = "object", properties = new { } },
                0),

            new ToolSpec(
                "view_audit_log",
                "查看最近的伺服器審核日誌（誰做了什麼管理動作）。當使用者問「最近誰做了什麼/操作紀錄」時呼叫。",
                new { type = "object", properties = new { } },
                PERM_VIEW_AUDIT_LOG),

            new ToolSpec(
                "send_message",
                "以 Bot 身分在指定頻道發送一則訊息。當使用者要求「幫我在某頻道發/公告XXX」時呼叫。",
                new
                {
                    type = "object",
                    properties = new
                    {
                        channel = new { type = "string", description = "目標頻道，使用 <#頻道ID> 格式" },
                        content = new { type = "string", description = "要發送的訊息內容（最多 2000 字）" }
                    },
                    required = new[] { "channel", "content" }
                },
                PERM_SEND_MESSAGES),

            // ── 中高風險：需二次確認（RequiresConfirmation = true）──────────────
            new ToolSpec(
                "kick_member",
                "將一位成員踢出伺服器（可重新加入）。當使用者要求「踢出/kick」某人時呼叫。",
                new
                {
                    type = "object",
                    properties = new { target = new { type = "string", description = "對象成員 <@使用者ID>" } },
                    required = new[] { "target" }
                },
                PERM_KICK_MEMBERS, RequiresConfirmation: true),

            new ToolSpec(
                "ban_member",
                "封鎖一位成員（永久禁止進入）。當使用者要求「封鎖/ban」某人時呼叫。",
                new
                {
                    type = "object",
                    properties = new
                    {
                        target = new { type = "string", description = "對象成員 <@使用者ID>" },
                        delete_message_days = new { type = "integer", description = "同時刪除其近幾天的訊息（0~7，預設 0）" }
                    },
                    required = new[] { "target" }
                },
                PERM_BAN_MEMBERS, RequiresConfirmation: true),

            new ToolSpec(
                "assign_role",
                "賦予一位成員某個身分組（僅限非特權身分組）。當使用者要求「給某人XX身分組/角色」時呼叫。",
                new
                {
                    type = "object",
                    properties = new
                    {
                        target = new { type = "string", description = "對象成員 <@使用者ID>" },
                        role = new { type = "string", description = "身分組，使用 <@&身分組ID> 格式" }
                    },
                    required = new[] { "target", "role" }
                },
                PERM_MANAGE_ROLES, RequiresConfirmation: true),

            new ToolSpec(
                "remove_role",
                "移除一位成員的某個身分組。當使用者要求「拿掉某人XX身分組/角色」時呼叫。",
                new
                {
                    type = "object",
                    properties = new
                    {
                        target = new { type = "string", description = "對象成員 <@使用者ID>" },
                        role = new { type = "string", description = "身分組 <@&身分組ID>" }
                    },
                    required = new[] { "target", "role" }
                },
                PERM_MANAGE_ROLES, RequiresConfirmation: true),

            new ToolSpec(
                "purge_messages",
                "批次刪除頻道最近 N 則訊息（僅 14 天內）。當使用者要求「清掉/刪除最近X則訊息」時呼叫。",
                new
                {
                    type = "object",
                    properties = new
                    {
                        count = new { type = "integer", description = "要刪除的則數（1~100）" },
                        channel = new { type = "string", description = "目標頻道 <#頻道ID>；不填則為目前頻道" }
                    },
                    required = new[] { "count" }
                },
                PERM_MANAGE_MESSAGES, RequiresConfirmation: true),

            new ToolSpec(
                "set_slowmode",
                "設定頻道慢速模式（每位成員發言間隔秒數）。當使用者要求「開慢速/設定慢速X秒」時呼叫。",
                new
                {
                    type = "object",
                    properties = new
                    {
                        seconds = new { type = "integer", description = "間隔秒數（0~21600，0=關閉）" },
                        channel = new { type = "string", description = "目標頻道 <#頻道ID>；不填則為目前頻道" }
                    },
                    required = new[] { "seconds" }
                },
                PERM_MANAGE_CHANNELS, RequiresConfirmation: true),

            new ToolSpec(
                "timeout_members",
                "一次禁言多位成員（批次）。當需要對『依條件找出的一群人』禁言時呼叫；單一明確對象請改用 timeout_member。",
                new
                {
                    type = "object",
                    properties = new
                    {
                        targets = new { type = "array", items = new { type = "string" }, description = "要禁言的成員清單，每個為 <@使用者ID> 提及格式" },
                        amount = new { type = "integer", description = "禁言時長數量（正整數）" },
                        unit = new { type = "string", @enum = new[] { "second", "minute", "hour", "day" }, description = "時長單位" },
                        reason = new { type = "string", description = "原因（顯示於確認與審計）" }
                    },
                    required = new[] { "targets", "amount", "unit" }
                },
                PERM_MODERATE_MEMBERS, RequiresConfirmation: true),

            new ToolSpec(
                "remove_timeout_members",
                "一次解除多位成員的禁言（批次）。當需要對一群人解除禁言時呼叫；單一對象請改用 remove_timeout。",
                new
                {
                    type = "object",
                    properties = new
                    {
                        targets = new { type = "array", items = new { type = "string" }, description = "要解除禁言的成員清單，每個為 <@使用者ID>" },
                        reason = new { type = "string", description = "原因（顯示於確認與審計）" }
                    },
                    required = new[] { "targets" }
                },
                PERM_MODERATE_MEMBERS, RequiresConfirmation: true),

            new ToolSpec(
                "fetch_recent_messages",
                "讀取目前頻道近 N 分鐘的訊息內容，用來判斷哪些成員符合條件（例如講髒話／不當發言）。" +
                "當使用者要對『符合某條件的一群人』操作、但沒有明確 @ 指定對象時，先呼叫此工具取得訊息，再自行判斷對象。",
                new
                {
                    type = "object",
                    properties = new
                    {
                        since_minutes = new { type = "integer", description = "往回讀取的分鐘數。只有當使用者明確講出具體時間（如「最近 30 分鐘」「過去 2 小時」「今天」）時才填對應數字；若使用者只說「最近」「剛剛」等沒有具體數字，請『不要』提供此參數（整個省略），系統會自動套用預設時間範圍，切勿自己猜一個數字。" }
                    }
                },
                0),

            // ── 信任名單（動態白名單）：只有「主人」(OwnerDiscordUserId) 能維護 ──────
            new ToolSpec(
                "add_trusted_user",
                "把一位使用者加入『主人的信任名單』。只有 Bot 的主人本人能使用。" +
                "當主人說「信任他／他是朋友／他是爹地／把他加入信任名單」時呼叫。加入前會跳出確認按鈕。",
                new
                {
                    type = "object",
                    properties = new
                    {
                        target = new { type = "string", description = "要信任的對象，使用 Discord 提及格式 <@使用者ID>" },
                        label = new { type = "string", description = "對象的稱呼/名字（例如「小明」），供記憶與顯示" },
                        tier = new { type = "string", description = "關係標籤（可選，例如「爹地」「朋友」「二爹地」）" }
                    },
                    required = new[] { "target" }
                },
                0, RequiresConfirmation: true),

            new ToolSpec(
                "remove_trusted_user",
                "把一位使用者從『主人的信任名單』移除。只有 Bot 的主人本人能使用。" +
                "當主人說「不要信任他了／把他移出信任名單」時呼叫。",
                new
                {
                    type = "object",
                    properties = new { target = new { type = "string", description = "要移除的對象 <@使用者ID>" } },
                    required = new[] { "target" }
                },
                0),

            new ToolSpec(
                "list_trusted_users",
                "列出『主人的信任名單』目前有哪些人。只有 Bot 的主人本人能查看。" +
                "當主人問「你信任哪些人／信任名單有誰」時呼叫。",
                new { type = "object", properties = new { } },
                0),

            new ToolSpec(
                "list_capabilities",
                "列出這個 Bot 能協助的所有功能。當使用者問「你能幫我做什麼/功能清單/使用說明/help」時呼叫。",
                new { type = "object", properties = new { } },
                0),
        };

        /// <summary>唯讀工具名單：agent 迴圈中可自動執行並把結果回灌給模型。</summary>
        private static readonly HashSet<string> ReadOnlyTools = new()
        {
            "fetch_recent_messages", "get_member_info", "get_member_status",
            "list_channels", "search_members", "list_roles", "view_audit_log", "list_capabilities",
            "list_trusted_users",
        };

        public bool IsReadOnly(string toolName) => ReadOnlyTools.Contains(toolName);

        /// <summary>執行唯讀工具，回傳要回灌給模型的文字。sinceMinutes/maxMessages 僅 fetch_recent_messages 使用。</summary>
        public async Task<string> ExecuteReadOnlyAsync(AiToolCall call, DiscordToolContext ctx, int sinceMinutes, int maxMessages, CancellationToken ct = default)
        {
            if (call.Name == "fetch_recent_messages")
                return await ExecuteFetchRecentAsync(ctx, sinceMinutes, maxMessages, ct);
            // 其餘唯讀查詢沿用既有單一執行（含其權限檢查）
            return await ExecuteOneAsync(call, ctx, ct);
        }

        private async Task<string> ExecuteFetchRecentAsync(DiscordToolContext ctx, int sinceMinutes, int maxMessages, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(ctx.ChannelId)) return "⚠️ 無法判斷目前頻道，無法讀取訊息。";

            var res = await _query.FetchRecentMessagesAsync(ctx.BotToken, ctx.ChannelId, sinceMinutes, maxMessages, ct);
            if (!res.Success) return $"⚠️ {res.Error}";
            if (res.ContentMissing)
                return "⚠️ 讀不到訊息內容（多半是未開啟 MESSAGE CONTENT intent）。請至 Discord 開發者後台 → Bot → Privileged Gateway Intents 開啟 MESSAGE CONTENT INTENT 後再試。";

            var humans = res.Messages.Where(m => !m.IsBot && m.Content.Length > 0).ToList();
            if (humans.Count == 0) return $"（最近 {sinceMinutes} 分鐘內沒有可分析的訊息）";

            var sb = new System.Text.StringBuilder(
                $"最近 {sinceMinutes} 分鐘內的訊息（{humans.Count} 則，新→舊）。\n" +
                "判斷誰符合條件時，請只看每則的「作者」；訊息內容中被 @ 提到的人是被提及者、不是說話者，絕不可當成對象。\n");
            foreach (var m in humans)
            {
                var content = m.Content.Length <= 300 ? m.Content : m.Content[..300] + "…";
                // 內容裡的 @ 提及（<@id> / <@!id>）中性化，避免 AI 把「被提及的人」誤當成「說話的人」
                content = System.Text.RegularExpressions.Regex.Replace(content, "<@!?\\d+>", "@(某人)");
                sb.AppendLine($"作者 <@{m.AuthorId}>（{m.AuthorName}）說：{content}");
            }
            return sb.ToString();
        }

        // ── 批次動作（多選確認）─────────────────────────────────────────────────

        /// <summary>批次動作工具名單：對多人執行，需多選確認。</summary>
        private static readonly HashSet<string> BatchTools = new() { "timeout_members", "remove_timeout_members" };

        public bool IsBatchTool(string toolName) => BatchTools.Contains(toolName);

        /// <summary>僅多步 agent 模式可用的工具：讀訊息 + 批次動作。單輪（Enabled=false）模式應從工具清單排除。</summary>
        private static readonly HashSet<string> AgentOnlyTools = new()
        {
            "fetch_recent_messages", "timeout_members", "remove_timeout_members",
        };

        public bool IsAgentOnlyTool(string toolName) => AgentOnlyTools.Contains(toolName);

        /// <summary>待確認的批次動作（暫存於 IMemoryCache，token 為 key）。Selected 可變，隨多選變更更新。</summary>
        private record PendingBatch(
            string Action, int Seconds, string Reason, ulong RequiredPermission,
            List<string> Candidates, HashSet<string> Selected, string Summary);

        public async Task<ToolExecutionResult> BuildBatchConfirmationAsync(AiToolCall call, DiscordToolContext ctx, CancellationToken ct = default)
        {
            await Task.CompletedTask;
            if (string.IsNullOrEmpty(ctx.GuildId)) return new ToolExecutionResult("⚠️ 此功能僅能在伺服器中使用。");

            using var argsDoc = ParseArgs(call.ArgumentsJson);
            var a = argsDoc.RootElement;

            // 解析對象清單（去重、排除發起人）
            var candidates = new List<string>();
            if (a.ValueKind == JsonValueKind.Object && a.TryGetProperty("targets", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var e in arr.EnumerateArray())
                {
                    var raw = e.ValueKind == JsonValueKind.String ? e.GetString() : e.ToString();
                    var id = ResolveSnowflake(raw);
                    if (id is null) continue;
                    if (id == ctx.InvokerUserId) continue;        // 排除發起人
                    if (!candidates.Contains(id)) candidates.Add(id);
                }
            }
            if (candidates.Count == 0) return new ToolExecutionResult("找不到符合條件的對象（或對象皆被排除）。");

            // 上限：對齊 Discord user select 硬上限 25，避免 MaxBatchTargets 被誤設 >25 時確認 UI 發不出（400）
            var cap = Math.Clamp(_agent.MaxBatchTargets, 1, 25);
            var capped = candidates.Count > cap;
            if (capped) candidates = candidates.Take(cap).ToList();

            // 動作參數
            string action, summary; int seconds = 0;
            if (call.Name == "timeout_members")
            {
                if (!TryGetInt(a, "amount", out var amount) || amount <= 0) return new ToolExecutionResult("⚠️ 請提供有效的禁言時長。");
                var s = ToSeconds(amount, GetString(a, "unit") ?? "minute");
                if (s is null) return new ToolExecutionResult("⚠️ 無法辨識時長單位。");
                seconds = Math.Clamp(s.Value, TIMEOUT_MIN_SECONDS, TIMEOUT_MAX_SECONDS);
                action = "timeout"; summary = $"禁言 {DescribeDuration(seconds)}";
            }
            else
            {
                action = "untimeout"; summary = "解除禁言";
            }
            var reason = GetString(a, "reason") ?? "AI 助理批次動作";

            var token = Guid.NewGuid().ToString("N")[..12];
            var pending = new PendingBatch(action, seconds, reason, PERM_MODERATE_MEMBERS,
                candidates, new HashSet<string>(candidates), summary);
            _cache.Set($"dcbatch:{token}", pending, TimeSpan.FromMinutes(Math.Max(1, _agent.PendingTtlMinutes)));

            var list = string.Join("、", candidates.Select(id => $"<@{id}>"));
            var text = $"🔍 找到 {candidates.Count} 人符合條件，將對他們執行：**{summary}**\n{list}\n"
                     + (capped ? $"（超過上限，已取前 {cap} 人）\n" : "")
                     + "取消勾選要排除的人後，按〔執行〕：";
            // 多選上限＝cap（已夾在 1~25），且 ≥ 已選候選數，讓管理員可在上限內增減
            return new ToolExecutionResult(text, BuildBatchComponents(token, candidates, cap));
        }

        public void UpdateBatchSelection(string customId, IReadOnlyList<string>? selectedIds)
        {
            if (!customId.StartsWith("selbatch:")) return;
            var token = customId["selbatch:".Length..];
            if (!_cache.TryGetValue<PendingBatch>($"dcbatch:{token}", out var pending) || pending is null) return;
            // 整體替換成新集合（非原地 Clear+Add），避免與「執行」在背景並行讀寫同一 HashSet 的 race；
            // 清單已僅限管理員操作（ProcessComponentAsync 把關），故管理員可自由增減對象。
            var selected = selectedIds is null ? new HashSet<string>() : new HashSet<string>(selectedIds);
            _cache.Set($"dcbatch:{token}", pending with { Selected = selected },
                TimeSpan.FromMinutes(Math.Max(1, _agent.PendingTtlMinutes)));
        }

        /// <summary>按下〔執行〕：取出暫存名單、重新檢查點擊者權限、逐一執行、回報並寫審計。</summary>
        private async Task<string> ExecuteBatchConfirmedAsync(string token, DiscordToolContext ctx, CancellationToken ct)
        {
            if (!_cache.TryGetValue<PendingBatch>($"dcbatch:{token}", out var pending) || pending is null)
                return "⚠️ 此確認已過期，請重新下指令。";

            // AllowAnyoneConfirm=false → 需具該動作權限
            if (!_agent.AllowAnyoneConfirm && !HasPermission(ctx.InvokerPermissions, pending.RequiredPermission))
                return NoPerm;

            var targets = pending.Selected.ToList();
            if (targets.Count == 0) { _cache.Remove($"dcbatch:{token}"); return "（沒有選取任何對象，未執行。）"; }

            int ok = 0; var failed = 0;
            foreach (var id in targets)
            {
                var r = pending.Action == "timeout"
                    ? await _moderation.TimeoutMemberAsync(ctx.BotToken, ctx.GuildId!, id, pending.Seconds, $"AI 批次（已確認）{pending.Reason}", ct)
                    : await _moderation.RemoveTimeoutAsync(ctx.BotToken, ctx.GuildId!, id, $"AI 批次（已確認）{pending.Reason}", ct);
                if (r.Success) ok++; else failed++;
                await Task.Delay(150, ct);   // 輕微間隔，降低 rate limit 風險
            }
            _cache.Remove($"dcbatch:{token}");

            _logger.LogInformation(
                "Discord 批次動作 Action={Action} Summary={Summary} 成功={Ok} 失敗={Fail} 發起者={Invoker} Guild={Guild} Channel={Channel}",
                pending.Action, pending.Summary, ok, failed, ctx.InvokerUserId, ctx.GuildId, ctx.ChannelId);

            var msg = $"✅ 已對 {ok} 人執行「{pending.Summary}」";
            if (failed > 0) msg += $"；{failed} 人略過（權限不足／階級高於 Bot／已離開）";
            return msg;
        }

        private static object BuildBatchComponents(string token, List<string> candidates, int maxValues) => new object[]
        {
            new
            {
                type = 1,   // action row：User Select（type 5）
                components = new object[]
                {
                    new
                    {
                        type = 5,
                        custom_id = $"selbatch:{token}",
                        min_values = 0,
                        max_values = maxValues,
                        default_values = candidates.Select(id => new { id, type = "user" }).ToArray(),
                    }
                }
            },
            new
            {
                type = 1,   // action row：執行 / 取消
                components = new object[]
                {
                    new { type = 2, style = 4, label = "執行", custom_id = $"cf:batch:{token}" },
                    new { type = 2, style = 2, label = "取消", custom_id = $"cf:batchx:{token}" },
                }
            }
        };

        public IReadOnlyList<AiFunctionTool> GetToolDefinitions() =>
            Specs.Select(s => new AiFunctionTool { Name = s.Name, Description = s.Description, Parameters = s.Parameters }).ToList();

        public async Task<ToolExecutionResult> ExecuteAsync(IReadOnlyList<AiToolCall> calls, DiscordToolContext ctx, CancellationToken cancellationToken = default)
        {
            // 含需確認的動作 → 回「確認訊息 + 按鈕」（取第一個；Discord 一則訊息只掛一組按鈕）
            var confirmCall = calls.FirstOrDefault(c => Specs.FirstOrDefault(s => s.Name == c.Name)?.RequiresConfirmation == true);
            if (confirmCall is not null)
                return await BuildConfirmationAsync(confirmCall, ctx, cancellationToken);

            var lines = new List<string>();
            foreach (var call in calls)
                lines.Add(await ExecuteOneAsync(call, ctx, cancellationToken));
            return new ToolExecutionResult(string.Join("\n", lines));
        }

        /// <summary>為需確認的動作組裝「確認訊息 + 按鈕」（先驗證權限與參數，把動作編進按鈕 custom_id）</summary>
        private async Task<ToolExecutionResult> BuildConfirmationAsync(AiToolCall call, DiscordToolContext ctx, CancellationToken ct)
        {
            var spec = Specs.First(s => s.Name == call.Name);
            if (string.IsNullOrEmpty(ctx.GuildId)) return new ToolExecutionResult("⚠️ 此功能僅能在伺服器中使用。");
            // RequiredPermission=0 的確認工具（如 add_trusted_user）改以工具內部自有閘門把關（主人身分），不套 Discord 權限
            if (spec.RequiredPermission != 0 && !HasPermission(ctx.InvokerPermissions, spec.RequiredPermission)) return new ToolExecutionResult("⚠️ 你沒有執行此動作的權限。");

            using var argsDoc = ParseArgs(call.ArgumentsJson);
            var a = argsDoc.RootElement;
            string? text = null, customId = null;

            switch (call.Name)
            {
                case "add_trusted_user":
                {
                    // 主人閘門：僅「系統角色為主人」的成員能維護（code 層，非靠 prompt）
                    var gate = await CheckOwnerAsync(ctx, ct);
                    if (gate is not null) { text = gate; break; }

                    var uid = ResolveTargetUserId(a, ctx);
                    if (uid is null) { text = "⚠️ 找不到要信任的對象，請用 @提及 指定。"; break; }

                    var label = GetString(a, "label")?.Trim();
                    var tier = GetString(a, "tier")?.Trim();
                    if (string.IsNullOrWhiteSpace(label)) label = null;
                    if (string.IsNullOrWhiteSpace(tier)) tier = null;

                    // label/tier 為任意中文，故用 token 暫存 cache，不塞進長度受限的 custom_id
                    var token = Guid.NewGuid().ToString("N")[..12];
                    _cache.Set($"dctrust:{token}", new PendingTrust(uid, label, tier),
                        TimeSpan.FromMinutes(Math.Max(1, _agent.PendingTtlMinutes)));

                    var who = label is null ? $"<@{uid}>" : $"<@{uid}>（{label}{(tier is null ? "" : "・" + tier)}）";
                    text = $"🤝 即將把 {who} 加入主人的信任名單，確認？";
                    customId = $"cf:trustadd:{token}";
                    break;
                }
                case "kick_member":
                {
                    var uid = ResolveTargetUserId(a, ctx);
                    if (uid is null) { text = "⚠️ 找不到要踢出的成員，請用 @提及 指定。"; break; }
                    text = $"⚠️ 即將將 <@{uid}> **踢出**伺服器，確認？";
                    customId = $"cf:kick:{uid}";
                    break;
                }
                case "ban_member":
                {
                    var uid = ResolveTargetUserId(a, ctx);
                    if (uid is null) { text = "⚠️ 找不到要封鎖的成員，請用 @提及 指定。"; break; }
                    var days = TryGetInt(a, "delete_message_days", out var d) ? Math.Clamp(d, 0, 7) : 0;
                    text = $"⚠️ 即將**封鎖** <@{uid}>（永久禁止進入），確認？";
                    customId = $"cf:ban:{uid}:{days}";
                    break;
                }
                case "assign_role":
                {
                    var uid = ResolveTargetUserId(a, ctx);
                    var rid = ResolveSnowflake(GetString(a, "role"));
                    if (uid is null || rid is null) { text = "⚠️ 找不到成員或身分組，請用 @提及 / 身分組提及指定。"; break; }
                    var perms = await _query.GetRolePermissionsAsync(ctx.BotToken, ctx.GuildId!, rid, ct);
                    if (perms is not null && (perms.Value & PRIVILEGED_ROLE_MASK) != 0)
                    { text = "⚠️ 基於安全，不可賦予帶有管理/封鎖/踢人等權限的身分組。"; break; }
                    text = $"⚠️ 即將賦予 <@{uid}> 身分組 <@&{rid}>，確認？";
                    customId = $"cf:roleadd:{uid}:{rid}";
                    break;
                }
                case "remove_role":
                {
                    var uid = ResolveTargetUserId(a, ctx);
                    var rid = ResolveSnowflake(GetString(a, "role"));
                    if (uid is null || rid is null) { text = "⚠️ 找不到成員或身分組。"; break; }
                    text = $"⚠️ 即將移除 <@{uid}> 的身分組 <@&{rid}>，確認？";
                    customId = $"cf:rolerm:{uid}:{rid}";
                    break;
                }
                case "purge_messages":
                {
                    var cid = ResolveSnowflake(GetString(a, "channel")) ?? ctx.ChannelId;
                    if (cid is null) { text = "⚠️ 找不到目標頻道。"; break; }
                    if (!TryGetInt(a, "count", out var cnt) || cnt < 1) { text = "⚠️ 請提供要刪除的則數。"; break; }
                    cnt = Math.Clamp(cnt, 1, PURGE_MAX);
                    text = $"⚠️ 即將刪除 <#{cid}> 最近 {cnt} 則訊息（僅 14 天內），確認？";
                    customId = $"cf:purge:{cid}:{cnt}";
                    break;
                }
                case "set_slowmode":
                {
                    var cid = ResolveSnowflake(GetString(a, "channel")) ?? ctx.ChannelId;
                    if (cid is null) { text = "⚠️ 找不到目標頻道。"; break; }
                    if (!TryGetInt(a, "seconds", out var secs) || secs < 0) { text = "⚠️ 請提供慢速秒數。"; break; }
                    secs = Math.Clamp(secs, 0, SLOWMODE_MAX);
                    text = secs == 0 ? $"⚠️ 即將**關閉** <#{cid}> 的慢速模式，確認？" : $"⚠️ 即將將 <#{cid}> 慢速設為 {secs} 秒，確認？";
                    customId = $"cf:slow:{cid}:{secs}";
                    break;
                }
            }

            return customId is null
                ? new ToolExecutionResult(text ?? "⚠️ 無法建立確認。")     // 參數錯誤 → 純文字、無按鈕
                : new ToolExecutionResult(text!, BuildConfirmComponents(customId));
        }

        public async Task<string?> ExecuteConfirmedAsync(string customId, DiscordToolContext ctx, CancellationToken ct = default)
        {
            if (customId == "cfx") return "已取消操作。";
            if (!customId.StartsWith("cf:")) return null;     // 非本服務的確認按鈕

            var p = customId.Split(':');
            if (p.Length < 3) return "⚠️ 確認資料無效。";
            if (string.IsNullOrEmpty(ctx.GuildId)) return "⚠️ 此操作僅限伺服器。";

            // 依動作「重新」檢查點擊者權限後執行
            switch (p[1])
            {
                case "trustadd":
                    return await ExecuteTrustAddConfirmedAsync(p[2], ctx, ct);
                case "batch":
                    return await ExecuteBatchConfirmedAsync(p[2], ctx, ct);
                case "batchx":
                    if (!_agent.AllowAnyoneConfirm && !HasPermission(ctx.InvokerPermissions, PERM_MODERATE_MEMBERS)) return NoPerm;
                    _cache.Remove($"dcbatch:{p[2]}");
                    return "已取消操作。";
                case "kick":
                    if (!HasPermission(ctx.InvokerPermissions, PERM_KICK_MEMBERS)) return NoPerm;
                    return Fmt(await _moderation.KickMemberAsync(ctx.BotToken, ctx.GuildId!, p[2], "AI 助理（已確認）踢出", ct), $"已將 <@{p[2]}> 踢出伺服器");
                case "ban":
                    if (!HasPermission(ctx.InvokerPermissions, PERM_BAN_MEMBERS)) return NoPerm;
                    return Fmt(await _moderation.BanMemberAsync(ctx.BotToken, ctx.GuildId!, p[2], ParseIntOr(p, 3, 0), "AI 助理（已確認）封鎖", ct), $"已封鎖 <@{p[2]}>");
                case "roleadd":
                    if (!HasPermission(ctx.InvokerPermissions, PERM_MANAGE_ROLES)) return NoPerm;
                    return Fmt(await _moderation.AddRoleAsync(ctx.BotToken, ctx.GuildId!, p[2], p[3], "AI 助理（已確認）賦予身分組", ct), $"已賦予 <@{p[2]}> 身分組 <@&{p[3]}>");
                case "rolerm":
                    if (!HasPermission(ctx.InvokerPermissions, PERM_MANAGE_ROLES)) return NoPerm;
                    return Fmt(await _moderation.RemoveRoleAsync(ctx.BotToken, ctx.GuildId!, p[2], p[3], "AI 助理（已確認）移除身分組", ct), $"已移除 <@{p[2]}> 的身分組 <@&{p[3]}>");
                case "purge":
                    if (!HasPermission(ctx.InvokerPermissions, PERM_MANAGE_MESSAGES)) return NoPerm;
                    var pr = await _moderation.PurgeMessagesAsync(ctx.BotToken, p[2], ParseIntOr(p, 3, 1), ct);
                    return pr.Success ? $"✅ {pr.Error ?? "已清除訊息"}" : $"❌ {pr.Error}";
                case "slow":
                    if (!HasPermission(ctx.InvokerPermissions, PERM_MANAGE_CHANNELS)) return NoPerm;
                    var secs = ParseIntOr(p, 3, 0);
                    return Fmt(await _moderation.SetSlowmodeAsync(ctx.BotToken, p[2], secs, "AI 助理（已確認）慢速", ct),
                        secs == 0 ? $"已關閉 <#{p[2]}> 的慢速模式" : $"已將 <#{p[2]}> 慢速設為 {secs} 秒");
                default:
                    return "⚠️ 未知的確認動作。";
            }
        }

        private const string NoPerm = "⚠️ 你沒有執行此動作的權限。";
        private static string Fmt(ModerationResult r, string successText) => r.Success ? $"✅ {successText}" : $"❌ {r.Error}";
        private static int ParseIntOr(string[] parts, int index, int fallback) =>
            parts.Length > index && int.TryParse(parts[index], out var v) ? v : fallback;

        private static object BuildConfirmComponents(string confirmCustomId) => new object[]
        {
            new
            {
                type = 1,   // action row
                components = new object[]
                {
                    new { type = 2, style = 4, label = "確認", custom_id = confirmCustomId },  // style 4 = danger
                    new { type = 2, style = 2, label = "取消", custom_id = "cfx" }              // style 2 = secondary
                }
            }
        };

        private async Task<string> ExecuteOneAsync(AiToolCall call, DiscordToolContext ctx, CancellationToken cancellationToken)
        {
            var spec = Specs.FirstOrDefault(s => s.Name == call.Name);
            if (spec is null)
                return $"⚠️ 未知的功能：{call.Name}";

            // HELP-01：直接回功能清單，不需權限/伺服器
            if (call.Name == "list_capabilities")
                return BuildCapabilitiesText();

            // 伺服器專屬動作：DM 無法執行
            if (string.IsNullOrEmpty(ctx.GuildId))
                return "⚠️ 此功能僅能在伺服器中使用，無法於私訊執行。";

            // 權限檢查：下指令者需具備該動作所需權限（ADMINISTRATOR 一律放行）
            if (spec.RequiredPermission != 0 && !HasPermission(ctx.InvokerPermissions, spec.RequiredPermission))
                return "⚠️ 你沒有執行此動作的權限。";

            using var args = ParseArgs(call.ArgumentsJson);
            return call.Name switch
            {
                "timeout_member" => await HandleTimeoutAsync(args.RootElement, ctx, cancellationToken),
                "remove_timeout" => await HandleRemoveTimeoutAsync(args.RootElement, ctx, cancellationToken),
                "move_member" => await HandleMoveAsync(args.RootElement, ctx, cancellationToken),
                "disconnect_voice" => await HandleDisconnectAsync(args.RootElement, ctx, cancellationToken),
                "voice_mute" => await HandleVoiceMuteAsync(args.RootElement, ctx, cancellationToken),
                "voice_deafen" => await HandleVoiceDeafenAsync(args.RootElement, ctx, cancellationToken),
                "set_nickname" => await HandleSetNicknameAsync(args.RootElement, ctx, cancellationToken),
                "unban_member" => await HandleUnbanAsync(args.RootElement, ctx, cancellationToken),
                "get_member_info" => await HandleQueryMemberAsync(args.RootElement, ctx, false, cancellationToken),
                "get_member_status" => await HandleQueryMemberAsync(args.RootElement, ctx, true, cancellationToken),
                "list_channels" => (await _query.ListChannelsAsync(ctx.BotToken, ctx.GuildId!, QUERY_LIST_LIMIT, cancellationToken)).Text,
                "search_members" => await HandleSearchMembersAsync(args.RootElement, ctx, cancellationToken),
                "list_roles" => (await _query.ListRolesAsync(ctx.BotToken, ctx.GuildId!, QUERY_LIST_LIMIT, cancellationToken)).Text,
                "view_audit_log" => (await _query.GetAuditLogAsync(ctx.BotToken, ctx.GuildId!, AUDIT_LOG_LIMIT, cancellationToken)).Text,
                "send_message" => await HandleSendMessageAsync(args.RootElement, ctx, cancellationToken),
                "remove_trusted_user" => await HandleRemoveTrustedAsync(args.RootElement, ctx, cancellationToken),
                "list_trusted_users" => await HandleListTrustedAsync(ctx, cancellationToken),
                _ => $"⚠️ 尚未實作的功能：{call.Name}"
            };
        }

        // ── 信任名單（動態白名單）────────────────────────────────────────────────

        /// <summary>待確認的「加入信任」動作（暫存於 IMemoryCache，token 為 key）。</summary>
        private record PendingTrust(string UserId, string? Label, string? Tier);

        /// <summary>主人閘門：信任名單維護僅限「系統角色為主人」的成員（可多人）。回 null=通過，否則為拒絕訊息。</summary>
        private async Task<string?> CheckOwnerAsync(DiscordToolContext ctx, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(ctx.InvokerUserId))
                return "⚠️ 無法辨識你的身分，無法維護信任名單。";
            if (!await _trust.IsOwnerAsync(ctx.BotBindingId, ctx.InvokerUserId, ct))
                return "⚠️ 只有名單中『系統角色為主人』的成員能維護信任名單。請主人先在後台指定主人，或由現任主人操作。";
            return null;
        }

        /// <summary>按下〔確認〕加入信任：重新把關主人身分、取出暫存資料、以「信任者」角色寫入名單。</summary>
        private async Task<string> ExecuteTrustAddConfirmedAsync(string token, DiscordToolContext ctx, CancellationToken ct)
        {
            var gate = await CheckOwnerAsync(ctx, ct);
            if (gate is not null) return gate;

            if (!_cache.TryGetValue<PendingTrust>($"dctrust:{token}", out var pending) || pending is null)
                return "⚠️ 此確認已過期，請重新下指令。";
            _cache.Remove($"dctrust:{token}");

            var user = new TrustedUser
            {
                Id = pending.UserId,
                Label = pending.Label ?? pending.UserId,
                Tier = pending.Tier,
                SystemRole = TrustRole.Trusted,   // 對話路徑只加「信任者」，不開放提權為主人
                GrantedBy = ctx.InvokerUserId,
                GrantedAt = DateTime.Now
            };
            var (added, saved) = await _trust.AddAsync(ctx.BotBindingId, user, ct);
            if (saved is null) return "❌ 加入失敗（名單可能已達上限，或 Bot 不存在）。";

            _logger.LogInformation("信任名單加入 BotBindingId={Bot} Target={Target} By={By}",
                ctx.BotBindingId, saved.Id, ctx.InvokerUserId);

            var who = $"<@{saved.Id}>" + (string.IsNullOrWhiteSpace(saved.Tier) ? "" : $"（{saved.Tier}）");
            return added ? $"✅ 已把 {who} 加入信任名單。" : $"✅ 已更新 {who} 的信任資料。";
        }

        /// <summary>移除信任對象（主人限定，立即執行、不需確認）。</summary>
        private async Task<string> HandleRemoveTrustedAsync(JsonElement args, DiscordToolContext ctx, CancellationToken ct)
        {
            var gate = await CheckOwnerAsync(ctx, ct);
            if (gate is not null) return gate;

            var uid = ResolveTargetUserId(args, ctx);
            if (uid is null) return "⚠️ 找不到要移除的對象，請用 @提及 指定。";

            var removed = await _trust.RemoveAsync(ctx.BotBindingId, uid, ct);
            if (removed)
                _logger.LogInformation("信任名單移除 BotBindingId={Bot} Target={Target} By={By}", ctx.BotBindingId, uid, ctx.InvokerUserId);
            return removed ? $"✅ 已把 <@{uid}> 移出信任名單。" : $"（<@{uid}> 本來就不在信任名單內。）";
        }

        /// <summary>列出信任名單（主人限定）。</summary>
        private async Task<string> HandleListTrustedAsync(DiscordToolContext ctx, CancellationToken ct)
        {
            var gate = await CheckOwnerAsync(ctx, ct);
            if (gate is not null) return gate;

            var list = await _trust.GetListAsync(ctx.BotBindingId, ct);
            if (list.Count == 0) return "目前信任名單是空的。";

            var sb = new StringBuilder($"目前信任名單（{list.Count} 人）：\n");
            foreach (var t in list)
            {
                var role = t.SystemRole == TrustRole.Owner ? "主人" : "信任者";
                sb.AppendLine($"・<@{t.Id}>{(string.IsNullOrWhiteSpace(t.Label) ? "" : $" {t.Label}")}{(string.IsNullOrWhiteSpace(t.Tier) ? "" : $"・{t.Tier}")}〔{role}〕");
            }
            return sb.ToString();
        }

        // ── 各工具處理 ────────────────────────────────────────────────────────

        private async Task<string> HandleTimeoutAsync(JsonElement args, DiscordToolContext ctx, CancellationToken ct)
        {
            var targetId = ResolveTargetUserId(args, ctx);
            if (targetId is null) return "⚠️ 找不到要禁言的成員，請用 @提及 指定對象。";

            if (!TryGetInt(args, "amount", out var amount) || amount <= 0)
                return "⚠️ 請提供有效的禁言時長。";
            var unit = GetString(args, "unit") ?? "second";

            var seconds = ToSeconds(amount, unit);
            if (seconds is null) return "⚠️ 不支援的時間單位。";
            if (seconds < TIMEOUT_MIN_SECONDS) return $"⚠️ 禁言時間最短 {TIMEOUT_MIN_SECONDS} 秒。";
            if (seconds > TIMEOUT_MAX_SECONDS) return "⚠️ 禁言時間最長 28 天。";

            var result = await _moderation.TimeoutMemberAsync(ctx.BotToken, ctx.GuildId!, targetId, seconds.Value, "AI 助理執行禁言", ct);
            return result.Success
                ? $"✅ 已將 <@{targetId}> 禁言 {DescribeDuration(seconds.Value)}"
                : $"❌ {result.Error}";
        }

        private async Task<string> HandleRemoveTimeoutAsync(JsonElement args, DiscordToolContext ctx, CancellationToken ct)
        {
            var targetId = ResolveTargetUserId(args, ctx);
            if (targetId is null) return "⚠️ 找不到要解除禁言的成員，請用 @提及 指定對象。";

            var result = await _moderation.RemoveTimeoutAsync(ctx.BotToken, ctx.GuildId!, targetId, "AI 助理解除禁言", ct);
            return result.Success ? $"✅ 已解除 <@{targetId}> 的禁言" : $"❌ {result.Error}";
        }

        private async Task<string> HandleMoveAsync(JsonElement args, DiscordToolContext ctx, CancellationToken ct)
        {
            var targetId = ResolveTargetUserId(args, ctx);
            if (targetId is null) return "⚠️ 找不到要移動的成員，請用 @提及 指定對象。";
            var channelId = ResolveSnowflake(GetString(args, "channel"));
            if (channelId is null) return "⚠️ 找不到目標語音頻道，請用 #頻道 指定。";

            var result = await _moderation.MoveMemberAsync(ctx.BotToken, ctx.GuildId!, targetId, channelId, "AI 助理移動語音", ct);
            return result.Success ? $"✅ 已將 <@{targetId}> 移動到 <#{channelId}>" : $"❌ {result.Error}";
        }

        private async Task<string> HandleDisconnectAsync(JsonElement args, DiscordToolContext ctx, CancellationToken ct)
        {
            var targetId = ResolveTargetUserId(args, ctx);
            if (targetId is null) return "⚠️ 找不到要中斷語音的成員，請用 @提及 指定對象。";

            var result = await _moderation.DisconnectVoiceAsync(ctx.BotToken, ctx.GuildId!, targetId, "AI 助理中斷語音", ct);
            return result.Success ? $"✅ 已將 <@{targetId}> 移出語音頻道" : $"❌ {result.Error}";
        }

        private async Task<string> HandleVoiceMuteAsync(JsonElement args, DiscordToolContext ctx, CancellationToken ct)
        {
            var targetId = ResolveTargetUserId(args, ctx);
            if (targetId is null) return "⚠️ 找不到對象成員，請用 @提及 指定。";
            var muted = GetBool(args, "muted") ?? true;

            var result = await _moderation.SetVoiceMuteAsync(ctx.BotToken, ctx.GuildId!, targetId, muted, "AI 助理語音禁麥", ct);
            return result.Success ? $"✅ 已{(muted ? "禁麥" : "解除禁麥")} <@{targetId}>" : $"❌ {result.Error}";
        }

        private async Task<string> HandleVoiceDeafenAsync(JsonElement args, DiscordToolContext ctx, CancellationToken ct)
        {
            var targetId = ResolveTargetUserId(args, ctx);
            if (targetId is null) return "⚠️ 找不到對象成員，請用 @提及 指定。";
            var deafened = GetBool(args, "deafened") ?? true;

            var result = await _moderation.SetVoiceDeafAsync(ctx.BotToken, ctx.GuildId!, targetId, deafened, "AI 助理語音禁聽", ct);
            return result.Success ? $"✅ 已{(deafened ? "禁聽" : "解除禁聽")} <@{targetId}>" : $"❌ {result.Error}";
        }

        private async Task<string> HandleSetNicknameAsync(JsonElement args, DiscordToolContext ctx, CancellationToken ct)
        {
            var targetId = ResolveTargetUserId(args, ctx);
            if (targetId is null) return "⚠️ 找不到對象成員，請用 @提及 指定。";
            var nickname = GetString(args, "nickname") ?? string.Empty;
            if (nickname.Length > NICKNAME_MAX_LENGTH) return $"⚠️ 暱稱最長 {NICKNAME_MAX_LENGTH} 字。";

            // 空字串 → 傳 null 給 Discord = 清除暱稱
            var nick = string.IsNullOrEmpty(nickname) ? null : nickname;
            var result = await _moderation.SetNicknameAsync(ctx.BotToken, ctx.GuildId!, targetId, nick, "AI 助理變更暱稱", ct);
            return result.Success
                ? (nick is null ? $"✅ 已清除 <@{targetId}> 的暱稱" : $"✅ 已將 <@{targetId}> 的暱稱改為「{nick}」")
                : $"❌ {result.Error}";
        }

        private async Task<string> HandleUnbanAsync(JsonElement args, DiscordToolContext ctx, CancellationToken ct)
        {
            var targetId = ResolveSnowflake(GetString(args, "target"));
            if (targetId is null) return "⚠️ 找不到要解除封鎖的使用者，請提供使用者 ID。";

            var result = await _moderation.UnbanMemberAsync(ctx.BotToken, ctx.GuildId!, targetId, "AI 助理解除封鎖", ct);
            return result.Success ? $"✅ 已解除對 <@{targetId}> 的封鎖" : $"❌ {result.Error}";
        }

        private async Task<string> HandleQueryMemberAsync(JsonElement args, DiscordToolContext ctx, bool statusOnly, CancellationToken ct)
        {
            var targetId = ResolveTargetUserId(args, ctx);
            if (targetId is null) return "⚠️ 找不到對象成員，請用 @提及 指定。";

            var result = statusOnly
                ? await _query.GetMemberStatusAsync(ctx.BotToken, ctx.GuildId!, targetId, ct)
                : await _query.GetMemberInfoAsync(ctx.BotToken, ctx.GuildId!, targetId, ct);
            return result.Text;
        }

        private async Task<string> HandleSearchMembersAsync(JsonElement args, DiscordToolContext ctx, CancellationToken ct)
        {
            var query = GetString(args, "query");
            if (string.IsNullOrWhiteSpace(query)) return "⚠️ 請提供要搜尋的名稱關鍵字。";

            var result = await _query.SearchMembersAsync(ctx.BotToken, ctx.GuildId!, query.Trim(), QUERY_SEARCH_LIMIT, ct);
            return result.Text;
        }

        private async Task<string> HandleSendMessageAsync(JsonElement args, DiscordToolContext ctx, CancellationToken ct)
        {
            var channelId = ResolveSnowflake(GetString(args, "channel"));
            if (channelId is null) return "⚠️ 找不到目標頻道，請用 #頻道 指定。";
            var content = GetString(args, "content");
            if (string.IsNullOrWhiteSpace(content)) return "⚠️ 請提供要發送的訊息內容。";
            if (content.Length > MESSAGE_MAX_LENGTH) return $"⚠️ 訊息最長 {MESSAGE_MAX_LENGTH} 字。";

            var result = await _moderation.SendMessageAsync(ctx.BotToken, channelId, content, ct);
            return result.Success ? $"✅ 已在 <#{channelId}> 發送訊息" : $"❌ {result.Error}";
        }

        private string BuildCapabilitiesText()
        {
            var sb = new StringBuilder("我目前可以協助你執行以下功能：\n");
            foreach (var s in Specs.Where(s => s.Name != "list_capabilities"))
                sb.AppendLine($"・{s.Description}");
            sb.Append("用自然語言告訴我即可，例如：「幫我禁言 @某人 10 分鐘」。");
            return sb.ToString();
        }

        // ── 共用工具 ──────────────────────────────────────────────────────────

        private static bool HasPermission(ulong perms, ulong required) =>
            (perms & PERM_ADMINISTRATOR) != 0 || (perms & required) != 0;

        private static JsonDocument ParseArgs(string json)
        {
            try { return JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json); }
            catch { return JsonDocument.Parse("{}"); }
        }

        /// <summary>
        /// 從工具參數取出目標使用者 ID。AI 應原樣帶入使用者訊息中的 &lt;@數字&gt; 提及，
        /// 故抽出數字並驗證為合法的 Discord snowflake（17~20 位）。
        /// 註：string option 內的提及 Discord 不一定會放進 resolved，因此以 snowflake 格式為準，
        /// 無效 ID 由 Discord API 的 404 攔截；resolved 若有則更佳（未來型別驗證用）。
        /// </summary>
        private static string? ResolveTargetUserId(JsonElement args, DiscordToolContext ctx)
        {
            var raw = GetString(args, "target");
            if (string.IsNullOrWhiteSpace(raw)) return null;

            return ResolveSnowflake(raw);
        }

        /// <summary>從 &lt;@id&gt; / &lt;#id&gt; / 純數字字串抽出合法的 Discord snowflake（17~20 位）</summary>
        private static string? ResolveSnowflake(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var id = new string(raw.Where(char.IsDigit).ToArray());
            return id.Length is >= 17 and <= 20 ? id : null;
        }

        private static int? ToSeconds(int amount, string unit) => unit.Trim().ToLowerInvariant() switch
        {
            "second" or "seconds" or "秒" => amount,
            "minute" or "minutes" or "分" or "分鐘" => amount * 60,
            "hour" or "hours" or "小時" => amount * 3600,
            "day" or "days" or "天" => amount * 86400,
            _ => null
        };

        private static string DescribeDuration(int seconds)
        {
            if (seconds % 86400 == 0) return $"{seconds / 86400} 天";
            if (seconds % 3600 == 0) return $"{seconds / 3600} 小時";
            if (seconds % 60 == 0) return $"{seconds / 60} 分鐘";
            return $"{seconds} 秒";
        }

        private static string? GetString(JsonElement el, string name) =>
            el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var p)
                ? (p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString())
                : null;

        private static bool TryGetInt(JsonElement el, string name, out int value)
        {
            value = 0;
            if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var p)) return false;
            if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out value)) return true;
            if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out value)) return true;
            return false;
        }

        private static bool? GetBool(JsonElement el, string name)
        {
            if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var p)) return null;
            return p.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(p.GetString(), out var b) => b,
                _ => null
            };
        }
    }
}