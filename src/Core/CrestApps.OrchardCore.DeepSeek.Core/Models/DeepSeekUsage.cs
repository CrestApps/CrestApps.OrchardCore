namespace CrestApps.OrchardCore.DeepSeek.Core.Models;

internal sealed class DeepSeekUsage
{
    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    public int TotalTokens { get; set; }

    public DeepSeekUsageDetails PromptTokensDetails { get; set; }

    public int PromptCacheHitTokens { get; set; }

    public int PromptCacheMissTokens { get; set; }
}
