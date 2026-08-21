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

        return 1;
    }
}
