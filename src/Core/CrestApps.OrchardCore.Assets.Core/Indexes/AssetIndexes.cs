using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Assets.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Assets.Core.Indexes;

/// <summary>
/// Indexes asset types by name and category.
/// </summary>
public sealed class AssetTypeIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the category.
    /// </summary>
    public string Category { get; set; }
}

/// <summary>
/// Indexes assets by number, name, type, owner, parent, state and serial number.
/// </summary>
public sealed class AssetIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the asset number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the type.
    /// </summary>
    public string AssetTypeId { get; set; }

    /// <summary>
    /// Gets or sets who owns it.
    /// </summary>
    public string OwnerKind { get; set; }

    /// <summary>
    /// Gets or sets the customer or company that owns it.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the parent asset.
    /// </summary>
    public string ParentAssetId { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the serial number.
    /// </summary>
    public string SerialNumber { get; set; }
}

/// <summary>
/// Indexes meter readings by asset, meter and time.
/// </summary>
public sealed class AssetMeterReadingIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the asset.
    /// </summary>
    public string AssetId { get; set; }

    /// <summary>
    /// Gets or sets the meter.
    /// </summary>
    public string Meter { get; set; }

    /// <summary>
    /// Gets or sets when it was read.
    /// </summary>
    public DateTime Utc { get; set; }
}

/// <summary>
/// Maps asset types.
/// </summary>
public sealed class AssetTypeIndexProvider : IndexProvider<AssetType>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssetTypeIndexProvider"/> class.
    /// </summary>
    public AssetTypeIndexProvider()
    {
        CollectionName = AssetsConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AssetType> context)
    {
        context.For<AssetTypeIndex>()
            .Map(item => new AssetTypeIndex { ItemId = item.ItemId, Name = item.Name, Category = item.Category.ToString() });
    }
}

/// <summary>
/// Maps assets.
/// </summary>
public sealed class AssetIndexProvider : IndexProvider<Asset>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssetIndexProvider"/> class.
    /// </summary>
    public AssetIndexProvider()
    {
        CollectionName = AssetsConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<Asset> context)
    {
        context.For<AssetIndex>()
            .Map(item => new AssetIndex
            {
                ItemId = item.ItemId,
                Number = item.Number,
                Name = item.Name,
                AssetTypeId = item.AssetTypeId,
                OwnerKind = item.OwnerKind.ToString(),
                OwnerId = item.OwnerId,
                ParentAssetId = item.ParentAssetId,
                Status = item.Status.ToString(),
                SerialNumber = item.SerialNumber,
            });
    }
}

/// <summary>
/// Maps meter readings.
/// </summary>
public sealed class AssetMeterReadingIndexProvider : IndexProvider<AssetMeterReading>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssetMeterReadingIndexProvider"/> class.
    /// </summary>
    public AssetMeterReadingIndexProvider()
    {
        CollectionName = AssetsConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<AssetMeterReading> context)
    {
        context.For<AssetMeterReadingIndex>()
            .Map(item => new AssetMeterReadingIndex { ItemId = item.ItemId, AssetId = item.AssetId, Meter = item.Meter, Utc = item.Utc });
    }
}

/// <summary>
/// Creates the Assets index tables.
/// </summary>
public static class AssetsIndexSchema
{
    /// <summary>
    /// Creates the tables and their indexes.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task that completes when the tables exist.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = AssetsConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<AssetTypeIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("Category", column => column.Nullable().WithLength(32)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<AssetTypeIndex>(table => table.CreateIndex("IDX_AssetTypeIndex_ItemId", "ItemId"), collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<AssetIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Number", column => column.Nullable().WithLength(64))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("AssetTypeId", column => column.Nullable().WithLength(26))
            .Column<string>("OwnerKind", column => column.Nullable().WithLength(20))
            .Column<string>("OwnerId", column => column.Nullable().WithLength(64))
            .Column<string>("ParentAssetId", column => column.Nullable().WithLength(26))
            .Column<string>("Status", column => column.Nullable().WithLength(20))
            .Column<string>("SerialNumber", column => column.Nullable().WithLength(128)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<AssetIndex>(table =>
        {
            table.CreateIndex("IDX_AssetIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_AssetIndex_Number", "Number");
            table.CreateIndex("IDX_AssetIndex_Type", "AssetTypeId", "Status");
            table.CreateIndex("IDX_AssetIndex_Owner", "OwnerKind", "OwnerId");
            table.CreateIndex("IDX_AssetIndex_Parent", "ParentAssetId");
            table.CreateIndex("IDX_AssetIndex_Serial", "SerialNumber");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<AssetMeterReadingIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("AssetId", column => column.Nullable().WithLength(26))
            .Column<string>("Meter", column => column.Nullable().WithLength(64))
            .Column<DateTime>("Utc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<AssetMeterReadingIndex>(table =>
        {
            table.CreateIndex("IDX_AssetMeterReadingIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_AssetMeterReadingIndex_Asset", "AssetId", "Meter", "Utc");
        }, collection: collection);
    }
}
