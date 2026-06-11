namespace ReactL.api.Models.BotBindings
{
    /// <summary>系統角色（功能權限）常數。</summary>
    public static class TrustRole
    {
        /// <summary>主人／管理者：可維護名單（可多人）。</summary>
        public const string Owner = "owner";
        /// <summary>信任者：受信任但不可維護名單。</summary>
        public const string Trusted = "trusted";

        /// <summary>正規化系統角色字串，非法值一律回 Trusted。</summary>
        public static string Normalize(string? role) =>
            string.Equals(role, Owner, System.StringComparison.OrdinalIgnoreCase) ? Owner : Trusted;
    }

    /// <summary>
    /// 信任系統的單一成員（序列化後存於 BotBinding.TrustedUsersJson 的 JSON 陣列）。
    /// 非 EF 實體，純為 JSON 結構。以 Discord User Id 為唯一鍵。
    /// 「主人」與「信任者」皆為此清單的成員，差別在 <see cref="SystemRole"/>。
    /// </summary>
    public class TrustedUser
    {
        /// <summary>對象的 Discord User ID（17~20 位 snowflake）</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>顯示名稱／暱稱（例如「小明」），供 prompt 與後台顯示</summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>關係（自訂情感標籤，例如「主人」「爹地」「朋友」），給角色語氣參考，可空</summary>
        public string? Tier { get; set; }

        /// <summary>系統角色（功能權限）：見 <see cref="TrustRole"/>。owner=可維護名單、trusted=僅受信任。</summary>
        public string SystemRole { get; set; } = TrustRole.Trusted;

        /// <summary>授權來源："admin"（後台）或操作的主人 Discord User ID（對話路徑）</summary>
        public string? GrantedBy { get; set; }

        /// <summary>加入時間</summary>
        public DateTime GrantedAt { get; set; }
    }
}