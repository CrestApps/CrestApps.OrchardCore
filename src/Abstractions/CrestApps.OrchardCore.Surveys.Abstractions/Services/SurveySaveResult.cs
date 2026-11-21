namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// The result of an autosave.
/// </summary>
public sealed class SurveySaveResult : SurveyResponseResult
{
    /// <summary>
    /// Gets or sets when the response was saved, in UTC.
    /// </summary>
    public DateTime? SavedUtc { get; set; }
}
