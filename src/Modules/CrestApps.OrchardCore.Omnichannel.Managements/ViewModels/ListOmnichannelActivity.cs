using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

public sealed class ListOmnichannelActivity : ShapeViewModel
{
    public ListOmnichannelActivity()
        : base("ListOmnichannelActivity")
    {
    }

    public ContentItem ContactContentItem { get; set; }

    [BindNever]
    public List<IShape> ScheduledContainers { get; set; }

    [BindNever]
    public IShape ScheduledPager { get; set; }

    [BindNever]
    public List<IShape> CompletedContainers { get; set; }

    [BindNever]
    public IShape CompletedPager { get; set; }
}
