using YesSql.Indexes;

namespace CrestApps.Data.YesSql.Indexes;

public abstract class CatalogItemIndex : MapIndex
{
    public string ItemId { get; set; }
}
