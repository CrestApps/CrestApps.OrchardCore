using CrestApps.OrchardCore.AI;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureWithAzureAISearchProfileSource : IAIChatProfileSource
{
    public const string Key = "AzureAISearch";

    public AzureWithAzureAISearchProfileSource(IStringLocalizer<AzureProfileSource> S)
    {
        DisplayName = S["Azure OpenAI with Azure AI Search"];
        Description = S["Provides AI profiles using Azure OpenAI models with your data."];
    }

    public string TechnicalName
        => Key;

    public string ProviderName
        => AzureOpenAIConstants.AzureProviderName;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
