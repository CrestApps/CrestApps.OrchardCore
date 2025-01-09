using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureWithAzureAISearchProfileSource : IOpenAIChatProfileSource
{
    public const string Key = "AzureAISearch";

    public AzureWithAzureAISearchProfileSource(IStringLocalizer<AzureProfileSource> S)
    {
        DisplayName = S["Azure OpenAI with Azure AI Search"];
        Description = S["AI-powered chat using Azure OpenAI models with data from Azure AI Search."];
    }

    public string TechnicalName => Key;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
