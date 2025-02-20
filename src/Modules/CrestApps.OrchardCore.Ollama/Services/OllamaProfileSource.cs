using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.AI.Ollama.Services;

public sealed class OllamaProfileSource : IAIProfileSource
{
    public const string ProviderTechnicalName = "Ollama";

    public const string ImplementationName = "Ollama";

    public OllamaProfileSource(IStringLocalizer<OllamaProfileSource> S)
    {
        DisplayName = S["Ollama"];
        Description = S["Provides AI profiles using Ollama."];
    }

    public string TechnicalName
        => ImplementationName;

    public string ProviderName
        => ProviderTechnicalName;

    public LocalizedString DisplayName { get; }

    public LocalizedString Description { get; }
}
