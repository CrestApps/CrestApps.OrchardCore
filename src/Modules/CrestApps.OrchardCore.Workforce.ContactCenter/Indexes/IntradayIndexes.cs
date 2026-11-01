using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Workforce.ContactCenter.Models;
using YesSql.Indexes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Indexes;

/// <summary>
/// Indexes intraday offers by team, status and time.
/// </summary>
public sealed class IntradayOfferIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the planning group.
    /// </summary>
    public string PlanningGroupId { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the start.
    /// </summary>
    public DateTime StartUtc { get; set; }
}

/// <summary>
/// Maps intraday offers.
/// </summary>
public sealed class IntradayOfferIndexProvider : IndexProvider<IntradayOffer>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IntradayOfferIndexProvider"/> class.
    /// </summary>
    public IntradayOfferIndexProvider()
    {
        CollectionName = WorkforceConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<IntradayOffer> context)
    {
        context.For<IntradayOfferIndex>()
            .Map(item => new IntradayOfferIndex
            {
                ItemId = item.ItemId,
                TeamId = item.TeamId,
                PlanningGroupId = item.PlanningGroupId,
                Status = item.Status.ToString(),
                StartUtc = item.StartUtc,
            });
    }
}

/// <summary>
/// Creates the intraday index tables.
/// </summary>
public static class IntradayIndexSchema
{
    /// <summary>
    /// Creates the tables and their indexes.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task that completes when the tables exist.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = WorkforceConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<IntradayOfferIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("TeamId", column => column.Nullable().WithLength(26))
            .Column<string>("PlanningGroupId", column => column.Nullable().WithLength(26))
            .Column<string>("Status", column => column.Nullable().WithLength(20))
            .Column<DateTime>("StartUtc"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<IntradayOfferIndex>(table =>
        {
            table.CreateIndex("IDX_IntradayOfferIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_IntradayOfferIndex_Team", "TeamId", "Status", "StartUtc");
            table.CreateIndex("IDX_IntradayOfferIndex_Group", "PlanningGroupId", "StartUtc");
        }, collection: collection);
    }
}
