using System.Data;

namespace CrestApps.OrchardCore.ContentTransfer;

public sealed class ContentExportContext : ImportContentContext
{
    public DataRow Row { get; set; }
}
