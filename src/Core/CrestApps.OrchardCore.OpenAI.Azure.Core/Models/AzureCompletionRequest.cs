using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public class AzureCompletionRequest
{
    public IEnumerable<ChatCompletionMessage> Messages { get; set; }

    public float? Temperature { get; set; }

    public float? TopP { get; set; }

    public float? MaxTokens { get; set; }

    public float? Stop { get; set; }

    public float? FrequencyPenalty { get; set; }

    public float? PresencePenalty { get; set; }

    public IList<CompletionDataSource> DataSources { get; set; }
}