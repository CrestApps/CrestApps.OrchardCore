using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// Represents the view model for contact navigation admin shape.
/// </summary>
public class ContactNavigationAdminShapeViewModel : ShapeViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactNavigationAdminShapeViewModel"/> class.
    /// </summary>
    public ContactNavigationAdminShapeViewModel()
    : base("ContactNavigationAdmin")
    {
    }

    /// <summary>
    /// Gets or sets the contact content item.
    /// </summary>
    public ContentItem ContactContentItem { get; set; }

    /// <summary>
    /// Gets or sets the definition.
    /// </summary>
    public ContentTypeDefinition Definition { get; set; }

    /// <summary>
    /// Gets or sets the show edit.
    /// </summary>
    public bool ShowEdit { get; set; }
}
