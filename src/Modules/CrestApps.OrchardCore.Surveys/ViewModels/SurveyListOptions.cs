namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The search and filters of the survey list.
/// </summary>
public class SurveyListOptions
{
    /// <summary>
    /// Gets or sets the search text.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the kind filter.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the status filter.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the tag filter.
    /// </summary>
    public string Tag { get; set; }

    /// <summary>
    /// Gets or sets which surveys show: standalone (empty) or all, owned ones included.
    /// </summary>
    public string Show { get; set; }
}
