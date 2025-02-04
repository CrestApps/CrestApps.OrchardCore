namespace CrestApps.OrchardCore.DeepSeek.Core.Models;

internal sealed class DeepSeekToolCall
{
    public string Id { get; set; }

    public string Type { get; set; }

    public DeepSeekFunctionCall Function { get; set; }
}
