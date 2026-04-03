using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

public class ContactNavigationAdminViewModel
{
    public ContentItem ContactContentItem { get; set; }

    public ContentTypeDefinition ContactContentTypeDefinition { get; set; }

    public bool ShowEdit { get; set; }
}
