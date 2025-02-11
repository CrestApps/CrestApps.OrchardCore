using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Ollama.Services;

public sealed class OllamaProfileSource : IAIProfileSource
{
    public const string Key = "Ollama";

    public OllamaProfileSource(IStringLocalizer<OllamaProfileSource> S)
    {
        DisplayName = S["Ollama"];
        Description = S["Provides AI profiles using Ollama."];
    }

    public string TechnicalName
        => Key;

    public string ProviderName
        => Key;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
