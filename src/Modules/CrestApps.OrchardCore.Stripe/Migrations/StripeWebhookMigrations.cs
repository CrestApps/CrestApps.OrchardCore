using CrestApps.OrchardCore.Stripe.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Stripe.Migrations;

public sealed class StripeWebhookMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ProcessedStripeWebhookEventIndex>(table => table
            .Column<string>("EventId", column => column.WithLength(66))
            .Column<DateTime>("ProcessedUtc")
        );

        await SchemaBuilder.AlterIndexTableAsync<ProcessedStripeWebhookEventIndex>(table => table
            .CreateIndex("IDX_ProcessedStripeWebhookEventIndex_EventId",
                "EventId")
        );

        return 1;
    }
}
