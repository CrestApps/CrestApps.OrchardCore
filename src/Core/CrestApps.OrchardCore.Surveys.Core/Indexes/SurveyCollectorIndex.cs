using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Finds collectors by survey and token.
/// </summary>
public sealed class SurveyCollectorIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the kind.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the token of the public link.
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets whether it is the default link.
    /// </summary>
    public bool IsDefault { get; set; }
}
