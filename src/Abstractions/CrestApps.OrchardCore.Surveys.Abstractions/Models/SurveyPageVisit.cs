namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// One visit of a page by a respondent.
/// </summary>
public sealed class SurveyPageVisit
{
    /// <summary>
    /// Gets or sets the id of the page.
    /// </summary>
    public string PageId { get; set; }

    /// <summary>
    /// Gets or sets when the respondent entered the page, in UTC.
    /// </summary>
    public DateTime EnteredUtc { get; set; }

    /// <summary>
    /// Gets or sets when the respondent left it, in UTC.
    /// </summary>
    public DateTime? LeftUtc { get; set; }
}
