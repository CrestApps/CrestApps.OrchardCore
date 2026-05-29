using System.Data;

namespace CrestApps.OrchardCore.ContentTransfer;

public sealed class ContentImportContext : ImportContentContext
{
    public DataColumnCollection Columns { get; set; }

    public DataRow Row { get; set; }
}
