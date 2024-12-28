using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public class AzureCompletionChoiceMessage : ChatCompletionMessage
{
    public bool EndTurn { get; set; }

    public AzureCompletionChoiceContext Context { get; set; }
}
