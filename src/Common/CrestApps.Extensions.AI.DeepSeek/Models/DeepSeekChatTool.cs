namespace CrestApps.Extensions.AI.DeepSeek.Models;

internal sealed class DeepSeekChatTool
{
    public string Type { get; set; } = "function";

    public DeepSeekChatFunction Function { get; set; }
}
