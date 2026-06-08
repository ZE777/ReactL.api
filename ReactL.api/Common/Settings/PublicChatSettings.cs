namespace ReactL.api.Common.Settings
{
    /// <summary>
    /// 前台公開聊天設定（存取碼閘門與用量配額）
    /// 對應 appsettings.json 的 "PublicChatSettings" section
    /// </summary>
    public class PublicChatSettings
    {
        /// <summary>是否要求有效存取碼才能使用公開聊天；false 時任何人皆可使用（仍受流量限制）</summary>
        public bool RequireAccessCode { get; set; } = true;

        /// <summary>全站每日 token 預算護欄（系統金鑰），超過則公開聊天暫停；0 = 不限制</summary>
        public int GlobalDailyTokenBudget { get; set; } = 0;

        /// <summary>後台新建存取碼時的預設每日 token 上限</summary>
        public int DefaultDailyTokenLimit { get; set; } = 50000;

        /// <summary>前台聊天記錄（PublicChatLogs）保留天數，逾期由背景服務清除；0 = 不清除（永久保留）</summary>
        public int LogRetentionDays { get; set; } = 30;
    }
}
