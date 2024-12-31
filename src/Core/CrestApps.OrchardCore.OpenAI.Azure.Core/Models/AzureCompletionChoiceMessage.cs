using CrestApps.OrchardCore.OpenAI.Models;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public class AzureCompletionChoiceMessage : ChatCompletionMessage
{
    public bool EndTurn { get; set; }

    public AzureCompletionChoiceContext Context { get; set; }

    public AzureFunctionCall FunctionCall { get; set; }
}

public class AzureFunctionCall
{
    public string Name { get; set; }

    public string Arguments { get; set; }
}
