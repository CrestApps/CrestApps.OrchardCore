namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public sealed class AzureCompletionUsage
{
    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int TotalTokens { get; set; }
}
