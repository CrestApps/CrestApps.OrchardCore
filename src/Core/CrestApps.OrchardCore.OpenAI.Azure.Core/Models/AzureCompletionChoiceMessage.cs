using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public sealed class AzureCompletionChoiceMessage : OpenAIChatCompletionMessage
{
    public bool EndTurn { get; set; }

    public AzureCompletionChoiceContext Context { get; set; }

    public AzureFunctionCall FunctionCall { get; set; }
}
