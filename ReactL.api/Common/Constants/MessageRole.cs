namespace ReactL.api.Common.Constants
{
    /// <summary>對話訊息角色常數，對應 AI 對話的標準角色定義</summary>
    public static class MessageRole
    {
        /// <summary>使用者輸入的訊息</summary>
        public const string User = "user";

        /// <summary>AI 模型回應的訊息</summary>
        public const string Assistant = "assistant";

        /// <summary>系統提示（System Prompt），設定 AI 的行為規則</summary>
        public const string System = "system";
    }
}
