namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// What an accommodation granted a response when it started: the closing date it moves the survey to and the extra
/// attempts. Its extra time is already in the response's deadline. A completion without the respondent's request (the
/// deadline task, the upkeep that submits responses of a closed survey) reads it when no user can be looked up, as on an
/// anonymous survey, so the response finishes under the same terms it started with.
/// </summary>
public sealed class SurveyResponseAccommodation
{
    /// <summary>
    /// Gets or sets the closing date the accommodation moves the survey to, in UTC, or <see langword="null"/> when it
    /// keeps the survey's.
    /// </summary>
    public DateTime? ClosesUtc { get; set; }

    /// <summary>
    /// Gets or sets the attempts the accommodation adds to the total limit.
    /// </summary>
    public int ExtraAttempts { get; set; }
}
