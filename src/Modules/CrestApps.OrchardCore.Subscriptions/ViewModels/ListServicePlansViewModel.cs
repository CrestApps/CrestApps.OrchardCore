using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents the service plan shapes and pager displayed on a service plan list.
/// </summary>
public class ListServicePlansViewModel
{
    /// <summary>
    /// Gets or sets the pager shape for the service plan list.
    /// </summary>
    public IShape Pager { get; set; }

    /// <summary>
    /// Gets or sets the service plan shapes to display.
    /// </summary>
    public IList<IShape> ServicePlans { get; set; }
}
