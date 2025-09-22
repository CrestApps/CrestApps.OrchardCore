using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

public sealed class CompleteOmnichannelActivityContainer : ShapeViewModel
{
    public CompleteOmnichannelActivityContainer()
        : base("CompleteOmnichannelActivityContainer")
    {
    }

    public IShape Activity { get; set; }

    public ContentItem ContactContentItem { get; set; }

    public IShape Subject { get; set; }
}
