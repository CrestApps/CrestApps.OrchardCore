using CrestApps.OrchardCore.DncRegistry.Models;

namespace CrestApps.OrchardCore.DncRegistry.ViewModels;

/// <summary>
/// View model for rendering a single local DNC list entry in summary admin display.
/// </summary>
public class LocalDncListViewModel
{
    /// <summary>
    /// Gets or sets the local DNC list entry.
    /// </summary>
    public LocalDncList LocalDncList { get; set; }
}
