using YesSql.Indexes;

namespace CrestApps.OrchardCore.YesSql.Core.Indexes;

public abstract class CatalogItemIndex : MapIndex
{
    public string ItemId { get; set; }
}
