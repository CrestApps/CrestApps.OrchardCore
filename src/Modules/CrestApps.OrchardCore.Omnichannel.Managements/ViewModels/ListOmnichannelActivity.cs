using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
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
    /// Gets or sets the scheduled activities and their pager, rendered by an admin list.
    /// </summary>
    [BindNever]
    public AdminListContext ScheduledList { get; set; }

    /// <summary>
    /// Gets or sets the completed activities and their pager, rendered by an admin list.
    /// </summary>
    [BindNever]
    public AdminListContext CompletedList { get; set; }
}
