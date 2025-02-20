using CrestApps.OrchardCore.AI;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureAISearchProfileSource : IAIProfileSource
{
    public const string ImplementationName = "AzureAISearch";

    public AzureAISearchProfileSource(IStringLocalizer<AzureProfileSource> S)
    {
        DisplayName = S["Azure OpenAI with Azure AI Search"];
        Description = S["Provides AI profiles using Azure OpenAI models with your data."];
    }

    public string TechnicalName
        => ImplementationName;

    public string ProviderName
        => AzureOpenAIConstants.ProviderName;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
