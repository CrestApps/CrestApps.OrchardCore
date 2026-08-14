using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for contact navigation admin.
/// </summary>
public class ContactNavigationAdminViewModel
{
    /// <summary>
    /// Gets or sets the contact content item.
    /// </summary>
    public ContentItem ContactContentItem { get; set; }

    /// <summary>
    /// Gets or sets the contact content type definition.
    /// </summary>
    public ContentTypeDefinition ContactContentTypeDefinition { get; set; }

    /// <summary>
    /// Gets or sets the show edit.
    /// </summary>
    public bool ShowEdit { get; set; }
}
