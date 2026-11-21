namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// The changes between two survey definitions.
/// </summary>
public sealed class SurveyDefinitionDiff
{
    /// <summary>
    /// Gets or sets the changes.
    /// </summary>
    public List<SurveyDefinitionChange> Changes { get; set; } = [];

    /// <summary>
    /// Gets whether a change may affect existing answers or comparisons.
    /// </summary>
    public bool HasDestructiveChanges => Changes.Any(change => change.Destructive);
}
