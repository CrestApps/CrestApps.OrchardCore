using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the dialer decision log: the <see cref="DialerDecisionIndex"/> and the
/// <see cref="DialerDecisionAgentIndex"/>.
/// </summary>
/// <remarks>
/// The tables start empty. Every decision event saved from now on is indexed as it is written, and the events written
/// before the tables existed are indexed by the dialer decision index backfill background task, which runs after the
/// tenant has started rather than here, so a tenant with a long event history does not wait on it to activate.
/// </remarks>
internal sealed class DialerDecisionIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the decision and decision agent index tables and the indexes the decision log reads them through.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<DialerDecisionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("EventType", column => column.WithLength(DialerDecisionIndexColumns.EventTypeLength))
            .Column<string>("QueueId", column => column.WithLength(DialerDecisionIndexColumns.QueueIdLength))
            .Column<string>("CampaignId", column => column.WithLength(DialerDecisionIndexColumns.IdLength))
            .Column<string>("ActivityItemId", column => column.WithLength(DialerDecisionIndexColumns.IdLength))
            .Column<string>("QueueItemId", column => column.WithLength(DialerDecisionIndexColumns.IdLength))
            .Column<string>("AgentId", column => column.WithLength(DialerDecisionIndexColumns.IdLength))
            .Column<bool>("Succeeded")
            .Column<string>("Reason", column => column.WithLength(DialerDecisionIndexColumns.ReasonLength))
            .Column<int>("OccurrenceCount")
            .Column<DateTime>("OccurredUtc", column => column.NotNull())
            .Column<DateTime>("LastSeenUtc", column => column.NotNull()),
            collection: ContactCenterStorage.CollectionName
        );

        // The period read: every decision last made in a range, optionally of one type.
        await IndexTableIndexCreation.CreateIfMissingAsync<DialerDecisionIndex>(
            SchemaBuilder,
            "IDX_DialerDecisionIndex_Time",
            ContactCenterStorage.CollectionName,
            "LastSeenUtc", "EventType", "DocumentId");

        // One queue's decisions, newest first: the report's queue filter, the queue's last decision, and the latest
        // decision a repeat is folded into.
        await IndexTableIndexCreation.CreateIfMissingAsync<DialerDecisionIndex>(
            SchemaBuilder,
            "IDX_DialerDecisionIndex_Queue",
            ContactCenterStorage.CollectionName,
            "QueueId", "EventType", "LastSeenUtc", "DocumentId");

        // The decisions made about one record.
        await IndexTableIndexCreation.CreateIfMissingAsync<DialerDecisionIndex>(
            SchemaBuilder,
            "IDX_DialerDecisionIndex_Activity",
            ContactCenterStorage.CollectionName,
            "ActivityItemId", "LastSeenUtc", "DocumentId");

        await IndexTableIndexCreation.CreateIfMissingAsync<DialerDecisionIndex>(
            SchemaBuilder,
            "IDX_DialerDecisionIndex_Campaign",
            ContactCenterStorage.CollectionName,
            "CampaignId", "LastSeenUtc", "DocumentId");

        // The backfill asks which of a page of events already has a row.
        await IndexTableIndexCreation.CreateIfMissingAsync<DialerDecisionIndex>(
            SchemaBuilder,
            "IDX_DialerDecisionIndex_DocumentId",
            ContactCenterStorage.CollectionName,
            "DocumentId");

        await SchemaBuilder.CreateMapIndexTableAsync<DialerDecisionAgentIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("AgentId", column => column.WithLength(DialerDecisionIndexColumns.IdLength))
            .Column<bool>("IsEligible")
            .Column<bool>("IsSelected")
            .Column<string>("EventType", column => column.WithLength(DialerDecisionIndexColumns.EventTypeLength))
            .Column<string>("QueueId", column => column.WithLength(DialerDecisionIndexColumns.QueueIdLength))
            .Column<string>("CampaignId", column => column.WithLength(DialerDecisionIndexColumns.IdLength))
            .Column<string>("ActivityItemId", column => column.WithLength(DialerDecisionIndexColumns.IdLength))
            .Column<bool>("Succeeded")
            .Column<string>("Reason", column => column.WithLength(DialerDecisionIndexColumns.ReasonLength))
            .Column<int>("OccurrenceCount")
            .Column<DateTime>("OccurredUtc", column => column.NotNull())
            .Column<DateTime>("LastSeenUtc", column => column.NotNull()),
            collection: ContactCenterStorage.CollectionName
        );

        // The decisions made about one agent, newest first.
        await IndexTableIndexCreation.CreateIfMissingAsync<DialerDecisionAgentIndex>(
            SchemaBuilder,
            "IDX_DialerDecisionAgentIndex_Agent",
            ContactCenterStorage.CollectionName,
            "AgentId", "LastSeenUtc", "DocumentId");

        await CreateReportIndexAsync();

        return 2;
    }

    /// <summary>
    /// Adds the index reports read the decisions through, newest first by <c>LastSeenUtc</c> a page at a time, so each
    /// page and each date range is found by seeking.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await CreateReportIndexAsync();

        return 2;
    }

    private Task<bool> CreateReportIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<DialerDecisionIndex>(
            SchemaBuilder,
            "IDX_DialerDecisionIndex_Report",
            ContactCenterStorage.CollectionName,
            "LastSeenUtc", "Id");
    }
}
