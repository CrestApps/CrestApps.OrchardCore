using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Surveys.ViewModels;

/// <summary>
/// The survey list.
/// </summary>
public class SurveyListViewModel
{
    /// <summary>
    /// Gets or sets the search and filters.
    /// </summary>
    public SurveyListOptions Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the list.
    /// </summary>
    public AdminListContext List { get; set; }
}
