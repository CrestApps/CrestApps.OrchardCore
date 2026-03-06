using CrestApps.OrchardCore.YesSql.Core.Indexes;

namespace CrestApps.OrchardCore.AI.Core.Indexes;

public sealed class AIDocumentChunkIndex : CatalogItemIndex
{
    public string AIDocumentId { get; set; }

    public string ReferenceId { get; set; }

    public string ReferenceType { get; set; }

    public int Index { get; set; }
}
