namespace CrestApps.OrchardCore.ContentTransfer.Models;

public sealed class ContentTypeTransferSettings
{
    public bool AllowBulkImport { get; set; } = true;

    public bool AllowBulkExport { get; set; } = true;
}
