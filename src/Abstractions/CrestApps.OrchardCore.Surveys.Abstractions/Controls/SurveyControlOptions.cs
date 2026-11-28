namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// The registered question types and presets.
/// </summary>
public sealed class SurveyControlOptions
{
    /// <summary>
    /// Gets the question types by name.
    /// </summary>
    public Dictionary<string, SurveyControlDescriptor> Controls { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the presets by name.
    /// </summary>
    public Dictionary<string, SurveyControlPresetDescriptor> Presets { get; } = new(StringComparer.OrdinalIgnoreCase);
}
