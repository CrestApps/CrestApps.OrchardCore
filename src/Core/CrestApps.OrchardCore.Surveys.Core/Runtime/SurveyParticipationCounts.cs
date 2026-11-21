namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// How many responses a respondent started or completed in each limit window.
/// </summary>
public sealed class SurveyParticipationCounts
{
    /// <summary>
    /// Gets or sets the started counts.
    /// </summary>
    public SurveyParticipationWindowCounts Started { get; set; } = new();

    /// <summary>
    /// Gets or sets the completed counts.
    /// </summary>
    public SurveyParticipationWindowCounts Completed { get; set; } = new();

    /// <summary>
    /// Gets or sets when the last response was completed, in UTC.
    /// </summary>
    public DateTime? LastCompletedUtc { get; set; }
}
