using CrestApps.OrchardCore.AI;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.OpenAI.Templates;

/// <summary>
/// A template for creating an autocomplete AI profile optimized for code completion scenarios.
/// </summary>
public sealed class AutoCompleteAIProfileTemplate : IAIProfileTemplate
{
    private readonly IStringLocalizer<AutoCompleteAIProfileTemplate> _localizer;

    public AutoCompleteAIProfileTemplate(IStringLocalizer<AutoCompleteAIProfileTemplate> localizer)
    {
        _localizer = localizer;
    }

    public string Name => "AutoComplete";

    public LocalizedString DisplayName => _localizer["AutoComplete"];

    public LocalizedString Description => _localizer["Optimized for code completion and autocomplete scenarios."];

    public string ProfileSource => "OpenAI";

    public Task ApplyAsync(AIProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        // Set profile type to Utility for non-conversational use
        profile.Type = AIProfileType.Utility;

        // Set system message optimized for code completion
        var metadata = profile.As<AIProfileMetadata>();
        metadata.SystemMessage = "You are an AI assistant specialized in code completion. Provide concise, accurate code suggestions and completions. Focus on syntax correctness and best practices.";

        // Optimize parameters for autocomplete scenarios
        metadata.Temperature = 0.2f; // Lower temperature for more deterministic completions
        metadata.MaxTokens = 500; // Limit token count for quick completions
        metadata.TopP = 0.9f;
        metadata.FrequencyPenalty = 0.0f;
        metadata.PresencePenalty = 0.0f;

        profile.Put(metadata);

        return Task.CompletedTask;
    }
}
