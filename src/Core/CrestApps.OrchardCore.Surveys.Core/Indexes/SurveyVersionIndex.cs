using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Finds the versions of a survey.
/// </summary>
public sealed class SurveyVersionIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the version number.
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Gets or sets when the version was published, in UTC.
    /// </summary>
    public DateTime PublishedUtc { get; set; }
}
