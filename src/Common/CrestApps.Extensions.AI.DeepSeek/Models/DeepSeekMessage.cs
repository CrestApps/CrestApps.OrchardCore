namespace CrestApps.Extensions.AI.DeepSeek.Models;

internal sealed class DeepSeekMessage
{
    public string Role { get; set; }

    public string Content { get; set; }

    public string Name { get; set; }

    public string ReasoningContent { get; set; }

    public DeepSeekToolCall[] ToolCalls { get; set; }

    public string ToolCallId { get; set; }
}
