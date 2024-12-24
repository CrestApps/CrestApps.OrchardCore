using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core;

public sealed class AzureOpenAIAzureAISearchProfileSource : IAIChatProfileSource
{
    public const string Key = "AzureOpenAIAzureAISearch";

    public AzureOpenAIAzureAISearchProfileSource(IStringLocalizer<AzureOpenAIProfileSource> S)
    {
        DisplayName = S["Azure OpenAI with Azure Search AI"];
        Description = S["AI-powered chat using Azure OpenAI models with data from Azure Search AI."];
    }

    public string TechnicalName => Key;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
