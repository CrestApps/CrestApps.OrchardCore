using System.Data;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.ContentTransfer;

public sealed class ContentPartImportMapContext : ImportContentPartContext
{
    public ContentItem ContentItem { get; set; }

    public ContentTransferEntry Entry { get; set; }

    public DataColumnCollection Columns { get; set; }

    public DataRow Row { get; set; }
}
