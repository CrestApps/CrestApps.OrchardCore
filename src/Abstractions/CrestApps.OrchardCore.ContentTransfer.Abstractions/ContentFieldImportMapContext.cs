using System.Data;

namespace CrestApps.OrchardCore.ContentTransfer;

public sealed class ContentFieldImportMapContext : ImportContentFieldContext
{
    public ContentTransferEntry Entry { get; set; }

    public DataColumnCollection Columns { get; set; }

    public DataRow Row { get; set; }
}
