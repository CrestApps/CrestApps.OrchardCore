namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public sealed class AzureCompletionChoiceContext
{
    public AzureCompletionChoiceCitation[] Citations { get; set; }

    public string Intent { get; set; }
}
