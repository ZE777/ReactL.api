namespace ReactL.api.Common.Settings
{
    /// <summary>
    /// 啟動時自動種子 Admin 帳號設定
    /// 對應 appsettings.json 的 "AdminSeedSettings" section。
    /// 僅在資料庫中尚無任何 Role=Admin 帳號時建立一支，並要求首次登入後強制改密。
    /// 預設密碼為敏感資訊，正式環境請以 User Secrets / 環境變數覆蓋。
    /// </summary>
    public class AdminSeedSettings
    {
        /// <summary>是否啟用自動種子 Admin</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>種子 Admin 的登入 Email</summary>
        public string Email { get; set; } = "admin@reactl.local";

        /// <summary>種子 Admin 的預設密碼（敏感，留空不寫死；由 appsettings.Development/Production.json 或環境變數提供；首登後強制變更）</summary>
        public string DefaultPassword { get; set; } = "";

        /// <summary>種子 Admin 的顯示名稱</summary>
        public string DisplayName { get; set; } = "管理員";
    }
}
