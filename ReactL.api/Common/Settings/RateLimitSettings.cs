namespace ReactL.api.Common.Settings
{
    /// <summary>
    /// 公開端點流量限制設定
    /// 對應 appsettings.json 的 "RateLimitSettings" section
    /// </summary>
    public class RateLimitSettings
    {
        /// <summary>
        /// 公開聊天端點每個來源 IP 每分鐘允許的請求數，超過即回 429
        /// </summary>
        public int PublicPerMinute { get; set; } = 15;

        /// <summary>
        /// 超過上限時的排隊數；0 = 立即拒絕（回 429），不排隊
        /// </summary>
        public int QueueLimit { get; set; } = 0;

        /// <summary>
        /// 是否信任反向代理的 X-Forwarded-For 以取得真實 client IP。
        /// IIS in-process 託管通常不需要（RemoteIpAddress 已正確）；
        /// 置於 Nginx / CDN / Cloudflare 等獨立反向代理後方時才設為 true。
        /// 注意：開啟後若未限制 KnownProxies，攻擊者可偽造 X-Forwarded-For 繞過每 IP 限制。
        /// </summary>
        public bool UseForwardedHeaders { get; set; } = false;
    }
}
