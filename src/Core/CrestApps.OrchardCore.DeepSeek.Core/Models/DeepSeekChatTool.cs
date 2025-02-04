namespace CrestApps.OrchardCore.DeepSeek.Core.Models;

internal sealed class DeepSeekChatTool
{
    public string Type { get; set; } = "function";

    public DeepSeekChatFunction Function { get; set; }
}
