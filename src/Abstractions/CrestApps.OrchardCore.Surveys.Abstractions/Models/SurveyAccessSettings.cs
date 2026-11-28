namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// Who may respond to a survey and when. These settings are live: they apply without a new version.
/// </summary>
public sealed class SurveyAccessSettings
{
    /// <summary>
    /// Gets or sets who may respond.
    /// </summary>
    public SurveyAudienceMode Audience { get; set; }

    /// <summary>
    /// Gets or sets the ids of the user audiences that may respond.
    /// </summary>
    public List<string> AudienceIds { get; set; } = [];

    /// <summary>
    /// Gets or sets whether responses record who responded.
    /// </summary>
    public AnonymityMode Anonymity { get; set; }

    /// <summary>
    /// Gets or sets whether identified responses record the IP address.
    /// </summary>
    public bool CollectIpAddress { get; set; }

    /// <summary>
    /// Gets or sets when the survey opens, in UTC, or null.
    /// </summary>
    public DateTime? OpensUtc { get; set; }

    /// <summary>
    /// Gets or sets when the survey closes, in UTC, or null.
    /// </summary>
    public DateTime? ClosesUtc { get; set; }

    /// <summary>
    /// Gets or sets the days and hours responses may start, in the site time zone, or null for any time.
    /// </summary>
    public WeeklySchedule Schedule { get; set; }

    /// <summary>
    /// Gets or sets how many responses each respondent may give.
    /// </summary>
    public RespondentLimits Limits { get; set; } = new();

    /// <summary>
    /// Gets or sets the most completed responses the survey accepts, or null.
    /// </summary>
    public int? MaxResponses { get; set; }

    /// <summary>
    /// Gets or sets whether respondents who are not signed in must pass a CAPTCHA.
    /// </summary>
    public bool RequireCaptcha { get; set; }

    /// <summary>
    /// Gets or sets a statement respondents must accept before they start, such as an honor statement or a consent, or
    /// <see langword="null"/> for none. A start that doesn't accept it is refused with
    /// <see cref="SurveyErrorCode.StatementNotAccepted"/>.
    /// </summary>
    public string Statement { get; set; }

    /// <summary>
    /// Gets or sets after how many idle hours an unfinished response is abandoned.
    /// </summary>
    public int IncompleteExpiryHours { get; set; } = 168;

    /// <summary>
    /// Gets or sets the fewest anonymous responses a report shows for a group; 0 turns the check off.
    /// </summary>
    public int MinimumGroupSize { get; set; } = 5;
}
