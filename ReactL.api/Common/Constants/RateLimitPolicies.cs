namespace ReactL.api.Common.Constants
{
    /// <summary>
    /// 流量限制原則名稱（對應 Program.cs AddRateLimiter 註冊的 policy）
    /// </summary>
    public static class RateLimitPolicies
    {
        /// <summary>公開端點：以來源 IP 為單位的固定視窗限流</summary>
        public const string PublicPerIp = "public-per-ip";
    }
}
