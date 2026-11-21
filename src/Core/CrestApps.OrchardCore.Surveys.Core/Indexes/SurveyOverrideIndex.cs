using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Finds accommodations by survey, user and audience.
/// </summary>
public sealed class SurveyOverrideIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the id of the user, or null.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the id of the audience, or null.
    /// </summary>
    public string AudienceId { get; set; }

    /// <summary>
    /// Gets or sets the context as Kind:Id, or null.
    /// </summary>
    public string ContextRef { get; set; }

    /// <summary>
    /// Gets or sets when it was granted, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
