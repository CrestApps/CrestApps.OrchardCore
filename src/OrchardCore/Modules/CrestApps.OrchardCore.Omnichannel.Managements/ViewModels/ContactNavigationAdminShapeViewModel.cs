using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

public class ContactNavigationAdminShapeViewModel : ShapeViewModel
{
    public ContactNavigationAdminShapeViewModel()
        : base("ContactNavigationAdmin")
    {
    }

    public ContentItem ContactContentItem { get; set; }

    public ContentTypeDefinition Definition { get; set; }

    public bool ShowEdit { get; set; }
}
