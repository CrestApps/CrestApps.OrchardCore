using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// What an access rule decides on.
/// </summary>
public sealed class SurveyAccessContext
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
    /// Gets or sets the collector.
    /// </summary>
    public SurveyCollector Collector { get; set; }

    /// <summary>
    /// Gets or sets the invitation, or <see langword="null"/>.
    /// </summary>
    public SurveyInvitation Invitation { get; set; }

    /// <summary>
    /// Gets or sets who responds.
    /// </summary>
    public SurveyRespondentIdentity Identity { get; set; } = new();

    /// <summary>
    /// Gets or sets the respondent key limits count by (a user id, an invitation id, a browser key or a pseudonymous
    /// key), or <see langword="null"/> when the respondent cannot be recognized.
    /// </summary>
    public string RespondentKey { get; set; }

    /// <summary>
    /// Gets or sets the context.
    /// </summary>
    public RecordRef? Context { get; set; }

    /// <summary>
    /// Gets or sets the response, when completing; <see langword="null"/> when starting.
    /// </summary>
    public SurveyResponse Response { get; set; }

    /// <summary>
    /// Gets or sets the most generous accommodation that applies, or <see langword="null"/>.
    /// </summary>
    public SurveyOverride Override { get; set; }

    /// <summary>
    /// Gets or sets the collector password entered, or <see langword="null"/>.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Gets or sets the current time, in UTC.
    /// </summary>
    public DateTime UtcNow { get; set; }

    /// <summary>
    /// Gets or sets whether this is a test response; limits and windows do not apply to it.
    /// </summary>
    public bool IsTest { get; set; }
}
