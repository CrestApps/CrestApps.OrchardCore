namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public sealed class AzureCompletionChoice
{
    public int Index { get; set; }

    public string FinishReason { get; set; }

    public AzureCompletionChoiceMessage Message { get; set; }
}