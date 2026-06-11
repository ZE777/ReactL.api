using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ReactL.api.Services.Webhooks
{
    /// <summary>Discord 伺服器唯讀查詢實作（手刻 HTTP GET，回傳格式化文字）</summary>
    public class DiscordQueryService : IDiscordQueryService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DiscordQueryService> _logger;

        private const string DiscordApiBase = "https://discord.com/api/v10";

        public DiscordQueryService(IHttpClientFactory httpClientFactory, ILogger<DiscordQueryService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<QueryResult> GetMemberInfoAsync(string botToken, string guildId, string userId, CancellationToken ct = default)
        {
            var (ok, json, status) = await GetAsync(botToken, $"/guilds/{guildId}/members/{userId}", ct);
            if (!ok) return Fail("查詢成員資訊", status);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var name = root.TryGetProperty("nick", out var nick) && nick.ValueKind == JsonValueKind.String
                ? nick.GetString()
                : (root.TryGetProperty("user", out var u) && u.TryGetProperty("username", out var un) ? un.GetString() : "(未知)");
            var joinedAt = root.TryGetProperty("joined_at", out var j) ? j.GetString() : null;
            var roleCount = root.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.Array ? roles.GetArrayLength() : 0;
            var timedOut = root.TryGetProperty("communication_disabled_until", out var t)
                && t.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(t.GetString(), out var until) && until > DateTimeOffset.UtcNow;

            var sb = new StringBuilder($"📋 <@{userId}> 的資訊：\n");
            sb.AppendLine($"・暱稱/名稱：{name}");
            if (joinedAt is not null) sb.AppendLine($"・加入時間：{FormatDate(joinedAt)}");
            sb.AppendLine($"・身分組數：{roleCount}");
            sb.Append($"・禁言狀態：{(timedOut ? "🔇 禁言中" : "正常")}");
            return new QueryResult(true, sb.ToString());
        }

        public async Task<string?> GetMemberDisplayNameAsync(string botToken, string guildId, string userId, CancellationToken ct = default)
        {
            var (ok, json, _) = await GetAsync(botToken, $"/guilds/{guildId}/members/{userId}", ct);
            if (!ok) return null;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 伺服器暱稱 > 全域顯示名 > 使用者名
            if (root.TryGetProperty("nick", out var nick) && nick.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(nick.GetString()))
                return nick.GetString();
            if (root.TryGetProperty("user", out var u))
            {
                if (u.TryGetProperty("global_name", out var gn) && gn.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(gn.GetString()))
                    return gn.GetString();
                if (u.TryGetProperty("username", out var un) && un.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(un.GetString()))
                    return un.GetString();
            }
            return null;
        }

        public async Task<QueryResult> GetMemberStatusAsync(string botToken, string guildId, string userId, CancellationToken ct = default)
        {
            // 禁言狀態（從 member 取 communication_disabled_until）
            string timeoutText;
            var (mOk, mJson, _) = await GetAsync(botToken, $"/guilds/{guildId}/members/{userId}", ct);
            if (mOk)
            {
                using var mDoc = JsonDocument.Parse(mJson);
                var timedOut = mDoc.RootElement.TryGetProperty("communication_disabled_until", out var t)
                    && t.ValueKind == JsonValueKind.String
                    && DateTimeOffset.TryParse(t.GetString(), out var until) && until > DateTimeOffset.UtcNow;
                timeoutText = timedOut ? $"🔇 禁言中（至 {FormatDate(t.GetString()!)}）" : "未禁言";
            }
            else timeoutText = "查無此成員（可能已離開伺服器）";

            // 封鎖狀態（GET ban：200=已封鎖、404=未封鎖）
            var (bOk, _, bStatus) = await GetAsync(botToken, $"/guilds/{guildId}/bans/{userId}", ct);
            var banText = bOk ? "⛔ 已封鎖" : (bStatus == HttpStatusCode.NotFound ? "未封鎖" : "（無法查詢封鎖狀態，可能缺少權限）");

            return new QueryResult(true, $"📋 <@{userId}> 狀態：\n・禁言：{timeoutText}\n・封鎖：{banText}");
        }

        public async Task<QueryResult> ListChannelsAsync(string botToken, string guildId, int limit, CancellationToken ct = default)
        {
            var (ok, json, status) = await GetAsync(botToken, $"/guilds/{guildId}/channels", ct);
            if (!ok) return Fail("列出頻道", status);

            using var doc = JsonDocument.Parse(json);
            var items = doc.RootElement.EnumerateArray()
                .Select(c => new
                {
                    Name = c.TryGetProperty("name", out var n) ? n.GetString() : "?",
                    Type = c.TryGetProperty("type", out var t) ? t.GetInt32() : -1
                })
                .Where(c => c.Type is 0 or 2)   // 0=文字, 2=語音（略過分類等）
                .Take(limit)
                .Select(c => $"・{(c.Type == 2 ? "🔊" : "#")} {c.Name}")
                .ToList();

            return items.Count == 0
                ? new QueryResult(true, "此伺服器沒有可列出的頻道。")
                : new QueryResult(true, "📋 頻道清單：\n" + string.Join("\n", items));
        }

        public async Task<QueryResult> SearchMembersAsync(string botToken, string guildId, string query, int limit, CancellationToken ct = default)
        {
            var q = Uri.EscapeDataString(query);
            var (ok, json, status) = await GetAsync(botToken, $"/guilds/{guildId}/members/search?query={q}&limit={limit}", ct);
            if (!ok) return Fail("搜尋成員", status);

            using var doc = JsonDocument.Parse(json);
            var items = doc.RootElement.EnumerateArray()
                .Select(m =>
                {
                    var nick = m.TryGetProperty("nick", out var nk) && nk.ValueKind == JsonValueKind.String ? nk.GetString() : null;
                    var uname = m.TryGetProperty("user", out var u) && u.TryGetProperty("username", out var un) ? un.GetString() : "?";
                    var id = m.TryGetProperty("user", out var u2) && u2.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                    var label = nick is null ? uname : $"{nick}（{uname}）";
                    return id is null ? $"・{label}" : $"・{label} <@{id}>";
                })
                .ToList();

            return items.Count == 0
                ? new QueryResult(true, $"找不到名稱含「{query}」的成員。")
                : new QueryResult(true, $"📋 符合「{query}」的成員：\n" + string.Join("\n", items));
        }

        public async Task<QueryResult> ListRolesAsync(string botToken, string guildId, int limit, CancellationToken ct = default)
        {
            var (ok, json, status) = await GetAsync(botToken, $"/guilds/{guildId}/roles", ct);
            if (!ok) return Fail("列出身分組", status);

            using var doc = JsonDocument.Parse(json);
            var items = doc.RootElement.EnumerateArray()
                .Select(r => r.TryGetProperty("name", out var n) ? n.GetString() : "?")
                .Where(n => n != "@everyone")
                .Take(limit)
                .Select(n => $"・{n}")
                .ToList();

            return items.Count == 0
                ? new QueryResult(true, "此伺服器沒有可列出的身分組。")
                : new QueryResult(true, "📋 身分組清單：\n" + string.Join("\n", items));
        }

        public async Task<QueryResult> GetAuditLogAsync(string botToken, string guildId, int limit, CancellationToken ct = default)
        {
            var (ok, json, status) = await GetAsync(botToken, $"/guilds/{guildId}/audit-logs?limit={limit}", ct);
            if (!ok) return Fail("查看審核日誌", status);

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("audit_log_entries", out var entries) || entries.GetArrayLength() == 0)
                return new QueryResult(true, "近期沒有審核日誌紀錄。");

            var items = entries.EnumerateArray()
                .Take(limit)
                .Select(e =>
                {
                    var action = e.TryGetProperty("action_type", out var a) ? a.GetInt32() : -1;
                    var userId = e.TryGetProperty("user_id", out var u) ? u.GetString() : null;
                    var actor = userId is null ? "(系統)" : $"<@{userId}>";
                    return $"・{actor} 執行了「{AuditActionName(action)}」";
                })
                .ToList();

            return new QueryResult(true, "📋 最近審核日誌：\n" + string.Join("\n", items));
        }

        public async Task<ulong?> GetRolePermissionsAsync(string botToken, string guildId, string roleId, CancellationToken ct = default)
        {
            var (ok, json, _) = await GetAsync(botToken, $"/guilds/{guildId}/roles", ct);
            if (!ok) return null;

            using var doc = JsonDocument.Parse(json);
            foreach (var r in doc.RootElement.EnumerateArray())
            {
                if (r.TryGetProperty("id", out var id) && id.GetString() == roleId
                    && r.TryGetProperty("permissions", out var perms)
                    && ulong.TryParse(perms.GetString(), out var value))
                    return value;
            }
            return null;
        }

        // ── 共用 ──────────────────────────────────────────────────────────────

        public async Task<RecentMessagesResult> FetchRecentMessagesAsync(
            string botToken, string channelId, int sinceMinutes, int maxMessages, CancellationToken ct = default)
        {
            var cutoff = DateTimeOffset.UtcNow.AddMinutes(-Math.Max(1, sinceMinutes));
            var collected = new List<RecentMessage>();
            string? before = null;
            bool humanSeen = false, nonEmptyHumanSeen = false;

            // Discord 回傳新→舊，每頁最多 100 則；以 before 游標往更舊翻
            while (collected.Count < maxMessages)
            {
                var path = $"/channels/{channelId}/messages?limit=100" + (before is null ? "" : $"&before={before}");
                var (ok, json, status) = await GetAsync(botToken, path, ct);
                if (!ok)
                    return new RecentMessagesResult(false, $"無法讀取頻道訊息（Discord 回應 {(int)status}）", collected, false);

                using var doc = JsonDocument.Parse(json);
                var arr = doc.RootElement;
                if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0) break;

                int pageCount = 0; string? lastId = null; bool reachedCutoff = false;
                foreach (var m in arr.EnumerateArray())
                {
                    pageCount++;
                    lastId = m.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                    var ts = m.TryGetProperty("timestamp", out var tEl) && DateTimeOffset.TryParse(tEl.GetString(), out var d)
                        ? d : DateTimeOffset.UtcNow;
                    if (ts < cutoff) { reachedCutoff = true; break; }

                    var author = m.TryGetProperty("author", out var aEl) ? aEl : default;
                    var authorId = author.ValueKind == JsonValueKind.Object && author.TryGetProperty("id", out var aid) ? (aid.GetString() ?? "") : "";
                    var authorName = author.ValueKind == JsonValueKind.Object && author.TryGetProperty("username", out var un) ? (un.GetString() ?? "") : "";
                    var isBot = author.ValueKind == JsonValueKind.Object && author.TryGetProperty("bot", out var bEl) && bEl.ValueKind == JsonValueKind.True;
                    var content = m.TryGetProperty("content", out var cEl) ? (cEl.GetString() ?? "") : "";

                    if (!isBot) { humanSeen = true; if (content.Length > 0) nonEmptyHumanSeen = true; }

                    collected.Add(new RecentMessage(authorId, authorName, isBot, content, ts));
                    if (collected.Count >= maxMessages) break;
                }

                before = lastId;
                if (reachedCutoff || pageCount < 100 || before is null) break; // 到 cutoff / 最後一頁 / 無游標
            }

            // MESSAGE CONTENT intent 偵測：有真人訊息卻內容全空
            var contentMissing = humanSeen && !nonEmptyHumanSeen;
            return new RecentMessagesResult(true, null, collected, contentMissing);
        }

        private async Task<(bool Ok, string Json, HttpStatusCode Status)> GetAsync(string botToken, string path, CancellationToken ct)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{DiscordApiBase}{path}");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bot", botToken.Trim());
                using var resp = await client.SendAsync(req, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode)
                    _logger.LogWarning("Discord 查詢 {Path} 失敗 {Status}: {Body}", path, (int)resp.StatusCode, body);
                return (resp.IsSuccessStatusCode, body, resp.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Discord 查詢 {Path} 例外", path);
                return (false, string.Empty, HttpStatusCode.ServiceUnavailable);
            }
        }

        private static QueryResult Fail(string action, HttpStatusCode status) => new(false, (int)status switch
        {
            403 => $"無法{action}：Bot 缺少對應權限",
            404 => $"無法{action}：找不到對象",
            _ => $"無法{action}（Discord 回應 {(int)status}）"
        });

        private static string FormatDate(string iso) =>
            DateTimeOffset.TryParse(iso, out var d) ? d.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : iso;

        /// <summary>審核日誌 action_type 對應名稱（僅列常見的；其餘回傳代號）</summary>
        private static string AuditActionName(int action) => action switch
        {
            1 => "更新伺服器",
            20 => "踢出成員",
            22 => "封鎖成員",
            23 => "解除封鎖",
            24 => "更新成員",
            25 => "變更成員身分組",
            26 => "移動語音",
            27 => "中斷語音",
            72 => "刪除訊息",
            _ => $"動作#{action}"
        };
    }
}