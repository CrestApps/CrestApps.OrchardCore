using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public sealed class AzureCompletionChoiceCitation
{
    public string Content { get; set; }

    public string Title { get; set; }

    public string Url { get; set; }

    [JsonPropertyName("filepath")]
    public string FilePath { get; set; }

    public string ChunkId { get; set; }
}
