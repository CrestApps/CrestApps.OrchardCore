using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model used to log a completed inbound activity for a contact.
/// </summary>
public class CreateInboundActivityViewModel
{
    /// <summary>
    /// Gets or sets the contact content item the inbound activity is logged against.
    /// </summary>
    public ContentItem ContactContentItem { get; set; }

    /// <summary>
    /// Gets or sets the selected inbound subject content type.
    /// </summary>
    public string SubjectContentType { get; set; }

    /// <summary>
    /// Gets or sets the available inbound subject content types.
    /// </summary>
    public IEnumerable<SelectListItem> SubjectContentTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether any inbound subject is configured.
    /// </summary>
    public bool HasInboundSubjects { get; set; }

    /// <summary>
    /// Gets or sets the rendered subject editor shown alongside the subject selector.
    /// </summary>
    public IShape Subject { get; set; }

    /// <summary>
    /// Gets or sets the rendered completion container that holds the contact, subject, and activity shapes.
    /// </summary>
    public IShape Container { get; set; }
}
