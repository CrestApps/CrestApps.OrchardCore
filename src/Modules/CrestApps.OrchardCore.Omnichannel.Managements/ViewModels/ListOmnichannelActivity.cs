using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the list omnichannel activity.
/// </summary>
public sealed class ListOmnichannelActivity : ShapeViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ListOmnichannelActivity"/> class.
    /// </summary>
    public ListOmnichannelActivity()
    : base("ListOmnichannelActivity")
    {
    }

    /// <summary>
    /// Gets or sets the contact content item.
    /// </summary>
    public ContentItem ContactContentItem { get; set; }

    /// <summary>
    /// Gets or sets the scheduled containers.
    /// </summary>
    [BindNever]
    public List<IShape> ScheduledContainers { get; set; }

    /// <summary>
    /// Gets or sets the scheduled pager.
    /// </summary>
    [BindNever]
    public IShape ScheduledPager { get; set; }

    /// <summary>
    /// Gets or sets the completed containers.
    /// </summary>
    [BindNever]
    public List<IShape> CompletedContainers { get; set; }

    /// <summary>
    /// Gets or sets the completed pager.
    /// </summary>
    [BindNever]
    public IShape CompletedPager { get; set; }
}
