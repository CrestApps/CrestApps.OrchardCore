using CrestApps.OrchardCore.Telephony.Sms.Core;
using CrestApps.OrchardCore.Telephony.Sms.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Telephony.Sms.Migrations;

/// <summary>
/// Creates the schema for the SMS portal index tables (conversations and number routes).
/// </summary>
internal sealed class SmsPortalMigrations : DataMigration
{
    /// <summary>
    /// Creates the SMS portal index tables.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<SmsConversationIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ServiceAddress", column => column.WithLength(TelephonySmsStorage.AddressLength))
            .Column<string>("CustomerAddress", column => column.WithLength(TelephonySmsStorage.AddressLength))
            .Column<string>("OwnerType", column => column.WithLength(32))
            .Column<string>("OwnerId", column => column.WithLength(26))
            .Column<string>("AssignedAgentId", column => column.WithLength(26))
            .Column<string>("AssignmentStatus", column => column.WithLength(32))
            .Column<string>("Status", column => column.WithLength(32))
            .Column<bool>("IsRead")
            .Column<DateTime>("LastMessageUtc"),
            collection: TelephonySmsStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<SmsConversationIndex>(table => table
            .CreateIndex("IDX_SmsConversationIndex_Addresses",
                "DocumentId",
                "ServiceAddress",
                "CustomerAddress"),
            collection: TelephonySmsStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<SmsConversationIndex>(table => table
            .CreateIndex("IDX_SmsConversationIndex_Owner",
                "DocumentId",
                "OwnerType",
                "OwnerId",
                "AssignedAgentId",
                "LastMessageUtc"),
            collection: TelephonySmsStorage.CollectionName
        );

        await SchemaBuilder.CreateMapIndexTableAsync<SmsNumberRouteIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("EndpointId", column => column.WithLength(26))
            .Column<string>("DialedNumber", column => column.WithLength(TelephonySmsStorage.AddressLength))
            .Column<string>("TargetType", column => column.WithLength(32))
            .Column<string>("TargetId", column => column.WithLength(26))
            .Column<bool>("Enabled"),
            collection: TelephonySmsStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<SmsNumberRouteIndex>(table => table
            .CreateIndex("IDX_SmsNumberRouteIndex_DialedNumber",
                "DocumentId",
                "DialedNumber",
                "Enabled"),
            collection: TelephonySmsStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<SmsNumberRouteIndex>(table => table
            .CreateIndex("IDX_SmsNumberRouteIndex_Endpoint",
                "DocumentId",
                "EndpointId"),
            collection: TelephonySmsStorage.CollectionName
        );

        await CreateTemplateTableAsync();
        await CreateBroadcastTableAsync();

        return 3;
    }

    /// <summary>
    /// Adds the canned-response template index table.
    /// </summary>
    public async Task<int> UpdateFrom1Async()
    {
        await CreateTemplateTableAsync();

        return 2;
    }

    /// <summary>
    /// Adds the broadcast index table.
    /// </summary>
    public async Task<int> UpdateFrom2Async()
    {
        await CreateBroadcastTableAsync();

        return 3;
    }

    private async Task CreateBroadcastTableAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<SmsBroadcastIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("Status", column => column.WithLength(32)),
            collection: TelephonySmsStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<SmsBroadcastIndex>(table => table
            .CreateIndex("IDX_SmsBroadcastIndex_Status",
                "DocumentId",
                "Status"),
            collection: TelephonySmsStorage.CollectionName
        );
    }

    private async Task CreateTemplateTableAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<SmsTemplateIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<bool>("Enabled"),
            collection: TelephonySmsStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<SmsTemplateIndex>(table => table
            .CreateIndex("IDX_SmsTemplateIndex_Name",
                "DocumentId",
                "Name",
                "Enabled"),
            collection: TelephonySmsStorage.CollectionName
        );
    }
}
