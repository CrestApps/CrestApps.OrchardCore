using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the shape view model for the bulk manage activities page.
/// </summary>
public class BulkManageOmnichannelActivityContainer : ShapeViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BulkManageOmnichannelActivityContainer"/> class.
    /// </summary>
    public BulkManageOmnichannelActivityContainer()
        : base("BulkManageOmnichannelActivityContainer")
    {
    }

    /// <summary>
    /// Gets or sets the filter header shape rendered by the display driver.
    /// </summary>
    [BindNever]
    public IShape Header { get; set; }

    /// <summary>
    /// Gets or sets the activity container shapes.
    /// </summary>
    [BindNever]
    public List<IShape> Containers { get; set; } = [];

    /// <summary>
    /// Gets or sets the activity item IDs corresponding to each container (same order).
    /// </summary>
    [BindNever]
    public List<string> ActivityItemIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the pager shape.
    /// </summary>
    [BindNever]
    public IShape Pager { get; set; }

    /// <summary>
    /// Gets or sets the total count of matching activities.
    /// </summary>
    [BindNever]
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the current page size for the grid.
    /// </summary>
    [BindNever]
    public int CurrentPageSize { get; set; }

    /// <summary>
    /// Gets or sets the available page size options for the grid.
    /// </summary>
    [BindNever]
    public IEnumerable<SelectListItem> PageSizeOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the bulk actions panel shape rendered by the display driver.
    /// </summary>
    [BindNever]
    public IShape BulkActions { get; set; }

}
