using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Finds question types and their handlers, scorers and analyzers by name, from lookups built once per scope.
/// </summary>
public sealed class SurveyControlRegistry : ISurveyControlRegistry
{
    private readonly SurveyControlOptions _options;
    private readonly Dictionary<string, ISurveyControlHandler> _handlers;
    private readonly Dictionary<string, ISurveyControlScorer> _scorers;
    private readonly Dictionary<string, ISurveyControlAnalyzer> _analyzers;
    private IReadOnlyList<SurveyControlDescriptor> _controls;
    private IReadOnlyList<SurveyControlPresetDescriptor> _presets;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyControlRegistry"/> class.
    /// </summary>
    /// <param name="options">The registered types.</param>
    /// <param name="handlers">The handlers.</param>
    /// <param name="scorers">The scorers.</param>
    /// <param name="analyzers">The analyzers.</param>
    public SurveyControlRegistry(
        IOptions<SurveyControlOptions> options,
        IEnumerable<ISurveyControlHandler> handlers,
        IEnumerable<ISurveyControlScorer> scorers,
        IEnumerable<ISurveyControlAnalyzer> analyzers)
    {
        _options = options.Value;
        _handlers = ToLookup(handlers, handler => handler.ControlType);
        _scorers = ToLookup(scorers, scorer => scorer.ControlType);
        _analyzers = ToLookup(analyzers, analyzer => analyzer.ControlType);
    }

    /// <inheritdoc />
    public IReadOnlyList<SurveyControlDescriptor> GetControls()
        => _controls ??= _options.Controls.Values
            .Where(descriptor => _handlers.ContainsKey(descriptor.Name))
            .OrderBy(descriptor => descriptor.Category, StringComparer.Ordinal)
            .ThenBy(descriptor => descriptor.Position)
            .ThenBy(descriptor => descriptor.Name, StringComparer.Ordinal)
            .ToList();

    /// <inheritdoc />
    public IReadOnlyList<SurveyControlPresetDescriptor> GetPresets()
        => _presets ??= _options.Presets.Values
            .Where(preset => GetControl(preset.BaseControl) is not null)
            .OrderBy(preset => preset.Category, StringComparer.Ordinal)
            .ThenBy(preset => preset.Position)
            .ToList();

    /// <inheritdoc />
    public SurveyControlDescriptor GetControl(string controlType)
        => !string.IsNullOrEmpty(controlType)
            && _options.Controls.TryGetValue(controlType, out var descriptor)
            && _handlers.ContainsKey(descriptor.Name)
            ? descriptor
            : null;

    /// <inheritdoc />
    public SurveyControlPresetDescriptor GetPreset(string name)
        => !string.IsNullOrEmpty(name) && _options.Presets.TryGetValue(name, out var preset) ? preset : null;

    /// <inheritdoc />
    public ISurveyControlHandler GetHandler(string controlType)
        => !string.IsNullOrEmpty(controlType) && _handlers.TryGetValue(controlType, out var handler) ? handler : null;

    /// <inheritdoc />
    public ISurveyControlScorer GetScorer(string controlType)
        => !string.IsNullOrEmpty(controlType) && _scorers.TryGetValue(controlType, out var scorer) ? scorer : null;

    /// <inheritdoc />
    public ISurveyControlAnalyzer GetAnalyzer(string controlType)
        => GetControl(controlType)?.Has(SurveyControlCapabilities.Aggregatable) == true
            && _analyzers.TryGetValue(controlType, out var analyzer)
            ? analyzer
            : null;

    private static Dictionary<string, T> ToLookup<T>(IEnumerable<T> items, Func<T, string> key)
    {
        var lookup = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var name = key(item);

            if (!string.IsNullOrEmpty(name))
            {
                // The last registration wins, so a module can replace a built-in implementation.
                lookup[name] = item;
            }
        }

        return lookup;
    }
}
