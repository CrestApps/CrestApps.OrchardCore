using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Inventory.Core.Indexes;

/// <summary>
/// Indexes stock locations by name and owner.
/// </summary>
public sealed class StockLocationIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the kind of owner.
    /// </summary>
    public string OwnerKind { get; set; }

    /// <summary>
    /// Gets or sets the owner.
    /// </summary>
    public string OwnerId { get; set; }
}

/// <summary>
/// Maps stock locations.
/// </summary>
public sealed class StockLocationIndexProvider : IndexProvider<StockLocation>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StockLocationIndexProvider"/> class.
    /// </summary>
    public StockLocationIndexProvider()
    {
        CollectionName = InventoryConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<StockLocation> context)
    {
        context.For<StockLocationIndex>()
            .Map(location => new StockLocationIndex
            {
                ItemId = location.ItemId,
                Name = location.Name,
                OwnerKind = location.OwnerKind,
                OwnerId = location.OwnerId,
            });
    }
}
