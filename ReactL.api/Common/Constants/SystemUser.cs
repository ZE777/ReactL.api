namespace ReactL.api.Common.Constants
{
    /// <summary>
    /// 系統保留識別碼。
    /// 系統用戶（不可登入）持有 Official Persona 與系統預設 AI Key，
    /// 作為「公開聊天室／無自帶 Key 的使用者」的 fallback 來源。
    /// </summary>
    public static class SystemUser
    {
        /// <summary>系統用戶固定 Id，與 SqlScripts 種子腳本一致</summary>
        public static readonly Guid Id = Guid.Parse("11111111-1111-1111-1111-111111111111");

        /// <summary>系統用戶 Email（保留值，不可用於登入）</summary>
        public const string Email = "system@reactl.internal";

        /// <summary>系統用戶顯示名稱</summary>
        public const string DisplayName = "系統";

        /// <summary>結構合法但不對應任何密碼的 bcrypt 值，使 BCrypt.Verify 一律回 false（無法登入）</summary>
        public const string UnusablePasswordHash = "$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWa";
    }
}
