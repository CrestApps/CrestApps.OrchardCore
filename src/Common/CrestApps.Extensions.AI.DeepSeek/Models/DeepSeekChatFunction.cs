namespace CrestApps.Extensions.AI.DeepSeek.Models;

internal sealed class DeepSeekChatFunction
{
    public string Name { get; set; }

    public string Description { get; set; }

    public DeepSeekChatFunctionParameters Parameters { get; set; }
}
