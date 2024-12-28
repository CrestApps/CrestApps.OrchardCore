namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Models;

public class AzureCompletionUsage
{
    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int TotalTokens { get; set; }
}
