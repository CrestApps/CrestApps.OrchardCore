using CrestApps.OrchardCore.AI;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.OpenAI.Azure.Core.Services;

public sealed class AzureProfileSource : IAIProfileSource
{
    public const string Key = "Azure";

    public AzureProfileSource(IStringLocalizer<AzureProfileSource> S)
    {
        DisplayName = S["Azure OpenAI"];
        Description = S["Provides AI profiles using Azure OpenAI models."];
    }

    public string TechnicalName
        => Key;

    public string ProviderName
        => AzureOpenAIConstants.AzureProviderName;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
