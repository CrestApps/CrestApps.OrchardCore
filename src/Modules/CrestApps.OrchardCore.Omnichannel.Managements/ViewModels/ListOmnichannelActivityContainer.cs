using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the list omnichannel activity container.
/// </summary>
public sealed class ListOmnichannelActivityContainer : ShapeViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ListOmnichannelActivityContainer"/> class.
    /// </summary>
    public ListOmnichannelActivityContainer()
    : base("ListOmnichannelActivityContainer")
    {
    }

    /// <summary>
    /// Gets or sets the header.
    /// </summary>
    [BindNever]
    public IShape Header { get; set; }

    /// <summary>
    /// Gets or sets the activities and the pager the admin list renders.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }
}
