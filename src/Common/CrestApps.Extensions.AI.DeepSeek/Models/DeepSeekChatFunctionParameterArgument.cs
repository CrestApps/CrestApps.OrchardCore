using System.Text.Json.Serialization;

namespace CrestApps.Extensions.AI.DeepSeek.Models;

internal sealed class DeepSeekChatFunctionParameterArgument
{
    public string Type { get; set; }

    public string Description { get; set; }

    public bool IsRequired { get; set; }

    public object DefaultValue { get; set; }

    [JsonPropertyName("enum")]
    public string[] Values { get; set; }
}
