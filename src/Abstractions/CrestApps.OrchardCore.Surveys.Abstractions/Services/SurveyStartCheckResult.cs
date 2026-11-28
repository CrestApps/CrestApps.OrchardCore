namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Whether a respondent may start a new response now, and how many attempts are left.
/// </summary>
public sealed class SurveyStartCheckResult
{
    /// <summary>
    /// Gets or sets the reason a start is refused, or <see cref="SurveyErrorCode.None"/> when it is allowed.
    /// </summary>
    public SurveyErrorCode Error { get; set; }

    /// <summary>
    /// Gets whether a new response may start.
    /// </summary>
    public bool CanStart => Error == SurveyErrorCode.None;

    /// <summary>
    /// Gets or sets the attempts the total limit leaves, accommodations included and counted as the limit counts
    /// (started or completed responses), or <see langword="null"/> when no total limit applies.
    /// </summary>
    public int? AttemptsLeft { get; set; }

    /// <summary>
    /// Gets or sets whether the start asks for a password (an access code).
    /// </summary>
    public bool NeedsPassword { get; set; }

    /// <summary>
    /// Gets or sets the statement the respondent must accept before starting, or <see langword="null"/>.
    /// </summary>
    public string Statement { get; set; }
}
