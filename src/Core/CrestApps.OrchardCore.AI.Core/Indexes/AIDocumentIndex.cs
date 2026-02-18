using CrestApps.OrchardCore.YesSql.Core.Indexes;

namespace CrestApps.OrchardCore.AI.Core.Indexes;

public sealed class AIDocumentIndex : CatalogItemIndex
{
    public string ReferenceId { get; set; }

    public string ReferenceType { get; set; }

    public string Extension { get; set; }
}
