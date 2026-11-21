using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Finds the counter of a survey by kind.
/// </summary>
public sealed class SurveyCounterIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the kind of counter.
    /// </summary>
    public string Kind { get; set; }
}
