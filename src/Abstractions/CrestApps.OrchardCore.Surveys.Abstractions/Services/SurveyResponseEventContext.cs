using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// What an observer hears about a response.
/// </summary>
public sealed class SurveyResponseEventContext
{
    /// <summary>
    /// Gets or sets the survey.
    /// </summary>
    public Survey Survey { get; set; }

    /// <summary>
    /// Gets or sets the version the response runs on.
    /// </summary>
    public SurveyVersion Version { get; set; }

    /// <summary>
    /// Gets or sets the response.
    /// </summary>
    public SurveyResponse Response { get; set; }

    /// <summary>
    /// Gets or sets the id of the page completed, for page events.
    /// </summary>
    public string PageId { get; set; }

    /// <summary>
    /// Gets or sets the score change, for score events.
    /// </summary>
    public ScoreChange ScoreChange { get; set; }

    /// <summary>
    /// Gets or sets the id of the user who caused the event, or <see langword="null"/> for the respondent.
    /// </summary>
    public string ActorId { get; set; }

    /// <summary>
    /// Gets or sets whether a workflow caused the event, so workflow events can ignore their own changes.
    /// </summary>
    public bool CausedByWorkflow { get; set; }
}
