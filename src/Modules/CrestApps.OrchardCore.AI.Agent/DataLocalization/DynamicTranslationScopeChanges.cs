using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Agent.DataLocalization;

/// <summary>
/// The dynamic translations the tools saved in the current scope. The Translations recipe step merges new
/// translations into the culture's translations read from the cached document, which is refreshed only once the scope
/// commits, while every tool call of one chat message runs in the same scope. Without this record, a second save in
/// the same message would start from the translations as they were before the first save and drop them; and reading
/// translations after a save in the same scope would cache the old ones for the whole site.
/// </summary>
internal sealed class DynamicTranslationScopeChanges
{
    private readonly Dictionary<string, Dictionary<(string Context, string Key), string>> _saved = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the record of the scope of a tool invocation.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The record, or a new empty one when none is registered.</returns>
    public static DynamicTranslationScopeChanges Get(IServiceProvider services)
        => services.GetService<DynamicTranslationScopeChanges>() ?? new DynamicTranslationScopeChanges();

    /// <summary>
    /// Gets whether translations were saved in this scope.
    /// </summary>
    public bool HasChanges => _saved.Count > 0;

    /// <summary>
    /// Gets the translations to send to the step that saves new ones: every translation saved for the culture earlier
    /// in this scope, with the new ones over them. The step merges, so sending a translation again is harmless.
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <param name="translations">The new translations.</param>
    /// <returns>The translations to save.</returns>
    public List<(string Context, string Key, string Value)> Combine(string culture, IEnumerable<(string Context, string Key, string Value)> translations)
    {
        var combined = _saved.TryGetValue(culture, out var saved)
            ? new Dictionary<(string Context, string Key), string>(saved)
            : [];

        foreach (var (context, key, value) in translations)
        {
            combined[(context, key)] = value;
        }

        return combined.Select(entry => (entry.Key.Context, entry.Key.Key, entry.Value)).ToList();
    }

    /// <summary>
    /// Records the translations of a culture once they are saved.
    /// </summary>
    /// <param name="culture">The culture.</param>
    /// <param name="translations">The saved translations.</param>
    public void Saved(string culture, IEnumerable<(string Context, string Key, string Value)> translations)
    {
        if (!_saved.TryGetValue(culture, out var saved))
        {
            _saved[culture] = saved = [];
        }

        foreach (var (context, key, value) in translations)
        {
            saved[(context, key)] = value;
        }
    }
}
