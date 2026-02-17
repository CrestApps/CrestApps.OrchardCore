using CrestApps.OrchardCore.YesSql.Core.Indexes;

namespace CrestApps.OrchardCore.AI.Core.Indexes;

public sealed class AIProfileDocumentIndex : CatalogItemIndex
{
    public string ProfileId { get; set; }

    public string Extension { get; set; }
}
