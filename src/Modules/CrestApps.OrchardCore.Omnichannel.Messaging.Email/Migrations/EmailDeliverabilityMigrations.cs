using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Migrations;

/// <summary>
/// Creates the tables of the delivery log, the suppression list and each address's sending state.
/// </summary>
internal sealed class EmailDeliverabilityMigrations : DataMigration
{
    private const string Collection = EmailChannelConstants.DeliverabilityCollectionName;

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<EmailDeliveryLogIndex>(table => table
            .Column<string>("AddressId", column => column.WithLength(EmailDeliveryLogIndex.AddressIdLength))
            .Column<int>("Kind")
            .Column<string>("Recipient", column => column.WithLength(EmailDeliveryLogIndex.RecipientLength))
            .Column<string>("RecipientDomain", column => column.WithLength(EmailDeliveryLogIndex.RecipientLength))
            .Column<string>("MessageId", column => column.WithLength(EmailDeliveryLogIndex.MessageIdLength))
            .Column<string>("EventId", column => column.WithLength(EmailDeliveryLogIndex.EventIdLength))
            .Column<bool>("IsBulk")
            .Column<DateTime>("OccurredUtc", column => column.NotNull()),
            collection: Collection);

        await SchemaBuilder.AlterIndexTableAsync<EmailDeliveryLogIndex>(table =>
        {
            // Sending limits and health count one address's entries of one kind in a time window.
            table.CreateIndex("IDX_EmailDeliveryLog_Address", "AddressId", "Kind", "OccurredUtc", "DocumentId");

            // Per-domain limits and deferrals narrow that to one receiving domain.
            table.CreateIndex("IDX_EmailDeliveryLog_Domain", "AddressId", "RecipientDomain", "Kind", "OccurredUtc");

            // Soft bounces are counted per recipient, and an event is traced to its sender by recipient.
            table.CreateIndex("IDX_EmailDeliveryLog_Recipient", "Recipient", "Kind", "OccurredUtc");

            table.CreateIndex("IDX_EmailDeliveryLog_Message", "MessageId", "Kind");
            table.CreateIndex("IDX_EmailDeliveryLog_Event", "EventId");
            table.CreateIndex("IDX_EmailDeliveryLog_Retention", "OccurredUtc", "DocumentId");
        },
            collection: Collection);

        await SchemaBuilder.CreateMapIndexTableAsync<EmailSuppressionIndex>(table => table
            .Column<string>("Address", column => column.WithLength(EmailDeliveryLogIndex.RecipientLength))
            .Column<int>("Reason")
            .Column<DateTime>("CreatedUtc", column => column.NotNull()),
            collection: Collection);

        await SchemaBuilder.AlterIndexTableAsync<EmailSuppressionIndex>(table =>
        {
            table.CreateIndex("IDX_EmailSuppression_Address", "Address", "DocumentId");
            table.CreateIndex("IDX_EmailSuppression_Created", "CreatedUtc", "DocumentId");
        },
            collection: Collection);

        await SchemaBuilder.CreateMapIndexTableAsync<EmailSendingStateIndex>(table => table
            .Column<string>("AddressId", column => column.WithLength(EmailDeliveryLogIndex.AddressIdLength)),
            collection: Collection);

        await SchemaBuilder.AlterIndexTableAsync<EmailSendingStateIndex>(table => table
            .CreateIndex("IDX_EmailSendingState_Address", "AddressId", "DocumentId"),
            collection: Collection);

        return 1;
    }
}
