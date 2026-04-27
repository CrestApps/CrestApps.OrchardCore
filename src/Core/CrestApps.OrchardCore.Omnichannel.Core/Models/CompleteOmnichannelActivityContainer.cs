using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents the complete omnichannel activity container.
/// </summary>
public sealed class CompleteOmnichannelActivityContainer : ShapeViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompleteOmnichannelActivityContainer"/> class.
    /// </summary>
    public CompleteOmnichannelActivityContainer()
    : base("CompleteOmnichannelActivityContainer")
    {
    }

    /// <summary>
    /// Gets or sets the activity.
    /// </summary>
    public IShape Activity { get; set; }

    /// <summary>
    /// Gets or sets the contact content item.
    /// </summary>
    public ContentItem ContactContentItem { get; set; }

    /// <summary>
    /// Gets or sets the subject.
    /// </summary>
    public IShape Subject { get; set; }
}
