using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentTransfer;

public class ImportContentContext
{
    public ContentItem ContentItem { get; set; }

    public ContentTypeDefinition ContentTypeDefinition { get; set; }

    public ContentTransferEntry Entry { get; set; }
}
