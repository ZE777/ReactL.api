namespace ReactL.api.DTOs.Ai
{
    /// <summary>
    /// 提供給 AI 的「可呼叫工具」定義（OpenAI function-calling 格式的中性表示）。
    /// Parameters 為 JSON Schema 物件，描述此工具接受的參數。
    /// </summary>
    public class AiFunctionTool
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        /// <summary>JSON Schema 物件（序列化後即 OpenAI tools[].function.parameters）</summary>
        public object Parameters { get; set; } = new { type = "object", properties = new { } };
    }

    /// <summary>AI 單一工具呼叫（模型決定要呼叫哪個工具、帶什麼參數）</summary>
    public class AiToolCall
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>模型回傳的參數（原始 JSON 字串，由呼叫端依工具 schema 解析）</summary>
        public string ArgumentsJson { get; set; } = "{}";
    }

    /// <summary>
    /// 帶工具的 AI 呼叫結果：
    /// 若模型決定使用工具，ToolCalls 有值；否則回傳純文字 TextReply。
    /// </summary>
    public class AiToolResult
    {
        public List<AiToolCall> ToolCalls { get; set; } = new();
        public string? TextReply { get; set; }
        public int TokensIn { get; set; }
        public int TokensOut { get; set; }

        public bool HasToolCalls => ToolCalls.Count > 0;
    }
}