namespace CrestApps.OrchardCore.Surveys.Core.Results;

/// <summary>
/// The index facts of one response, read without its document.
/// </summary>
public sealed class SurveyResponseRow
{
    /// <summary>
    /// Gets or sets the id of the response.
    /// </summary>
    public string ResponseId { get; set; }

    /// <summary>
    /// Gets or sets the status, as stored.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the id of the user, or <see langword="null"/>.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the version number.
    /// </summary>
    public int VersionNumber { get; set; }

    /// <summary>
    /// Gets or sets when it started, in UTC.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it ended, in UTC, or <see langword="null"/>.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the percent, or <see langword="null"/>.
    /// </summary>
    public decimal? Percent { get; set; }

    /// <summary>
    /// Gets or sets whether it passed, or <see langword="null"/>.
    /// </summary>
    public bool? Passed { get; set; }
}
