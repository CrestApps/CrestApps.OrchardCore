namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// How long responses are kept.
/// </summary>
public sealed class SurveyRetentionSettings
{
    /// <summary>
    /// Gets or sets after how many days completed and abandoned responses are removed, or null to keep them.
    /// </summary>
    public int? Days { get; set; }

    /// <summary>
    /// Gets or sets whether old responses are deleted or anonymized.
    /// </summary>
    public RetentionAction Action { get; set; }
}
