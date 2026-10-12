using CrestApps.OrchardCore.Stripe.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Stripe.Migrations;

/// <summary>
/// Creates the database schema used to track processed Stripe webhook events.
/// </summary>
public sealed class StripeWebhookMigrations : DataMigration
{
    /// <summary>
    /// Creates the processed Stripe webhook event index table and its event identifier index.
    /// </summary>
    /// <returns>The migration version after the schema is created.</returns>
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
