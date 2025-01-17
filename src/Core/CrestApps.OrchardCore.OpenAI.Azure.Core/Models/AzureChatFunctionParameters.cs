using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

internal class AzureChatFunctionParameters
{
    public string Type { get; set; } = "object";

    public Dictionary<string, AzureChatFunctionParameterArgument> Properties { get; set; }

    public IEnumerable<string> Required { get; set; }
}

internal class AzureChatFunctionParameterArgument
{
    public string Type { get; set; }

    public string Description { get; set; }

    public bool IsRequired { get; set; }

    [JsonPropertyName("enum")]
    public string[] Values { get; set; }
}
