using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core;

public sealed class AzureOpenAIProfileSource : IAIChatProfileSource
{
    public const string Key = "AzureOpenAI";

    public AzureOpenAIProfileSource(IStringLocalizer<AzureOpenAIProfileSource> S)
    {
        DisplayName = S["Azure OpenAI"];
        Description = S["AI-powered chat using Azure OpenAI models."];
    }

    public string TechnicalName => Key;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
