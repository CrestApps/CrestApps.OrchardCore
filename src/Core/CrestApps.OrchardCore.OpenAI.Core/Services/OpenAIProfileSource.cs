using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.OpenAI.Services;

public sealed class OpenAIProfileSource : IAIProfileSource
{
    public const string ProviderTechnicalName = "OpenAI";

    public const string ImplementationName = "OpenAI";

    public OpenAIProfileSource(IStringLocalizer<OpenAIProfileSource> S)
    {
        DisplayName = S["OpenAI"];
        Description = S["Provides AI profiles using OpenAI."];
    }

    public string TechnicalName
        => ImplementationName;

    public string ProviderName
        => ProviderTechnicalName;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
