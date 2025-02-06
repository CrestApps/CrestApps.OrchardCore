namespace CrestApps.Extensions.AI.DeepSeek.Models;

internal sealed class DeepSeekChatFunctionParameters
{
    public string Type { get; set; } = "object";

    public Dictionary<string, DeepSeekChatFunctionParameterArgument> Properties { get; set; }

    public IEnumerable<string> Required { get; set; }
}
