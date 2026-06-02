namespace ReactL.api.Common.Constants
{
    /// <summary>
    /// Token 消耗來源常數，用於統計頁面分類 Token 使用量
    /// 對應 TokenUsageStat.Source 欄位
    /// </summary>
    public static class TokenSource
    {
        /// <summary>後台管理介面的測試對話</summary>
        public const string Admin = "admin";

        /// <summary>前台 Web 聊天室</summary>
        public const string Web = "web";

        /// <summary>LINE Bot 觸發的對話</summary>
        public const string Line = "line";

        /// <summary>Discord Bot 觸發的對話</summary>
        public const string Discord = "discord";
    }
}
