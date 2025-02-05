namespace CrestApps.OrchardCore.DeepSeek.Core.Models;

internal sealed class DeepSeekChatFunction
{
    public string Name { get; set; }

    public string Description { get; set; }

    public DeepSeekChatFunctionParameters Parameters { get; set; }
}
