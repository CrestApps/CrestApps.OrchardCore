namespace CrestApps.OrchardCore.DeepSeek.Core.Models;

internal sealed class DeepSeekChatFunctionParameters
{
    public string Type { get; set; } = "object";

    public Dictionary<string, DeepSeekChatFunctionParameterArgument> Properties { get; set; }

    public IEnumerable<string> Required { get; set; }
}
