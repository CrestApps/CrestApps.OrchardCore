using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.OpenAI.Services;

public sealed class OpenAIProfileSource : IAIProfileSource
{
    public const string Key = "OpenAI";

    public OpenAIProfileSource(IStringLocalizer<OpenAIProfileSource> S)
    {
        DisplayName = S["OpenAI"];
        Description = S["Provides AI profiles using OpenAI."];
    }

    public string TechnicalName
        => Key;

    public string ProviderName
        => Key;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
