using CrestApps.OrchardCore.OpenAI.Models;
using CrestApps.OrchardCore.OpenAI.Tools.Functions;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public sealed class AzureCompletionRequest
{
    public IEnumerable<OpenAIChatCompletionMessage> Messages { get; set; }

    public float? Temperature { get; set; }

    public float? TopP { get; set; }

    public float? MaxTokens { get; set; }

    public float? Stop { get; set; }

    public float? FrequencyPenalty { get; set; }

    public float? PresencePenalty { get; set; }

    public IList<AzureCompletionDataSource> DataSources { get; set; }

    public IEnumerable<IOpenAIChatFunction> Functions { get; set; }
}
