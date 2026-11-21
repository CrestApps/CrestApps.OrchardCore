using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// A read-only version.
/// </summary>
public class SurveyVersionViewModel
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the version, or <see langword="null"/> for the draft.
    /// </summary>
    public SurveyVersion Version { get; set; }

    /// <summary>
    /// Gets or sets the definition shown.
    /// </summary>
    public SurveyDefinition Definition { get; set; }

    /// <summary>
    /// Gets or sets the names shown of the question types, by name.
    /// </summary>
    public IReadOnlyDictionary<string, string> ControlNames { get; set; } = new Dictionary<string, string>();
}
