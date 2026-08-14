namespace CrestApps.OrchardCore.Reports.ViewModels;

/// <summary>
/// The view model for the Reports landing page, listing the reports the current user can access.
/// </summary>
public sealed class ReportsIndexViewModel
{
    /// <summary>
    /// Gets or sets the reports the current user is authorized to view, ordered by category and name.
    /// </summary>
    public IList<IReport> Reports { get; set; } = [];
}
