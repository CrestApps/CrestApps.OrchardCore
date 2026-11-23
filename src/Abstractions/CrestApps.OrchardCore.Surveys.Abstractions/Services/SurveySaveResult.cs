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

    /// <summary>
    /// Gets or sets the errors by question id of an autosave that was refused and saved nothing: with immediate feedback,
    /// the answers that would take an answer already checked off the respondent's path or hide it.
    /// </summary>
    public Dictionary<string, List<string>> Errors { get; set; } = [];
}
