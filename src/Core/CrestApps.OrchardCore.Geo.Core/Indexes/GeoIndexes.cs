using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Geo.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Geo.Core.Indexes;

/// <summary>
/// Indexes territories by name and parent.
/// </summary>
public sealed class ServiceTerritoryIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the parent.
    /// </summary>
    public string ParentId { get; set; }
}

/// <summary>
/// Indexes geocoding requests by their record, state and next attempt.
/// </summary>
public sealed class GeocodeRequestIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the kind of record.
    /// </summary>
    public string TargetType { get; set; }

    /// <summary>
    /// Gets or sets the record.
    /// </summary>
    public string TargetId { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets when to try next.
    /// </summary>
    public DateTime NextAttemptUtc { get; set; }
}

/// <summary>
/// Maps territories.
/// </summary>
public sealed class ServiceTerritoryIndexProvider : IndexProvider<ServiceTerritory>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ServiceTerritoryIndexProvider"/> class.
    /// </summary>
    public ServiceTerritoryIndexProvider()
    {
        CollectionName = GeoConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ServiceTerritory> context)
    {
        context.For<ServiceTerritoryIndex>()
            .Map(item => new ServiceTerritoryIndex { ItemId = item.ItemId, Name = item.Name, ParentId = item.ParentId });
    }
}

/// <summary>
/// Maps geocoding requests.
/// </summary>
public sealed class GeocodeRequestIndexProvider : IndexProvider<GeocodeRequest>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeocodeRequestIndexProvider"/> class.
    /// </summary>
    public GeocodeRequestIndexProvider()
    {
        CollectionName = GeoConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<GeocodeRequest> context)
    {
        context.For<GeocodeRequestIndex>()
            .Map(item => new GeocodeRequestIndex
            {
                ItemId = item.ItemId,
                TargetType = item.TargetType,
                TargetId = item.TargetId,
                Status = item.Status.ToString(),
                NextAttemptUtc = item.NextAttemptUtc,
            });
    }
}

/// <summary>
/// Creates the Geo index tables.
/// </summary>
public static class GeoIndexSchema
{
    /// <summary>
    /// Creates the tables and their indexes.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task that completes when the tables exist.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = GeoConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<ServiceTerritoryIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("ParentId", column => column.Nullable().WithLength(26)),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<ServiceTerritoryIndex>(table =>
        {
            table.CreateIndex("IDX_ServiceTerritoryIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_ServiceTerritoryIndex_Parent", "ParentId");
        }, collection: collection);

        await schemaBuilder.CreateMapIndexTableAsync<GeocodeRequestIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("TargetType", column => column.Nullable().WithLength(64))
            .Column<string>("TargetId", column => column.Nullable().WithLength(64))
            .Column<string>("Status", column => column.Nullable().WithLength(20))
            .Column<DateTime>("NextAttemptUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<GeocodeRequestIndex>(table =>
        {
            table.CreateIndex("IDX_GeocodeRequestIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_GeocodeRequestIndex_Target", "TargetType", "TargetId");
            table.CreateIndex("IDX_GeocodeRequestIndex_Due", "Status", "NextAttemptUtc");
        }, collection: collection);
    }
}
