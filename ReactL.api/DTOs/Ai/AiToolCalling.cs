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
        /// <summary>
        /// 上游回傳的 tool_call id。多步 agent 迴圈把工具結果以 role=tool 回灌時，
        /// 需以此 id 對應（tool_call_id）。單發呼叫可忽略。
        /// </summary>
        public string Id { get; set; } = string.Empty;

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

    /// <summary>agent 迴圈中，呼叫端對「單一工具呼叫」的處置種類。</summary>
    public enum AgentToolKind
    {
        /// <summary>唯讀工具：已執行，Text 為要回灌給模型續推的結果。</summary>
        ReadOnly,
        /// <summary>動作工具（非唯讀）：迴圈停止，交由呼叫端執行（收進 TerminalToolCalls）。</summary>
        Action,
        /// <summary>硬停止：直接以 Text 作為最終回覆結束（如「已超出系統設定規範」）。</summary>
        Stop,
    }

    /// <summary>agent 迴圈執行器對單一工具呼叫的回應。</summary>
    public record AgentToolResponse(AgentToolKind Kind, string? Text = null)
    {
        public static AgentToolResponse FromReadOnly(string text) => new(AgentToolKind.ReadOnly, text);
        public static AgentToolResponse Action() => new(AgentToolKind.Action);
        public static AgentToolResponse Stop(string text) => new(AgentToolKind.Stop, text);
    }

    /// <summary>
    /// 多步 agent 迴圈（RunToolAgentAsync）的結果。
    /// 迴圈會自動執行「唯讀工具」並把結果回灌給模型；遇到「非唯讀（動作）工具」即停止，
    /// 把該回合的工具呼叫交由呼叫端（例如 DiscordToolService.ExecuteAsync）處理。
    /// </summary>
    public class AiAgentResult
    {
        /// <summary>模型最終的純文字回覆（沒有要執行動作時）。</summary>
        public string? FinalText { get; set; }

        /// <summary>需由呼叫端執行的「動作」工具呼叫（非唯讀）；null 表示沒有。</summary>
        public List<AiToolCall>? TerminalToolCalls { get; set; }

        /// <summary>實際進行的迴圈步數。</summary>
        public int Steps { get; set; }

        public int TokensIn { get; set; }
        public int TokensOut { get; set; }

        /// <summary>是否因達步數上限或 token 預算而中止（未自然完成）。</summary>
        public bool Aborted { get; set; }

        public bool HasTerminalCalls => TerminalToolCalls is { Count: > 0 };
    }
}