using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Models;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Checkout.Core.Migrations;

/// <summary>
/// Creates the index tables that back checkout sessions, the durable payment attempt ledger, and the
/// durable refund ledger.
/// </summary>
public sealed class CheckoutMigrations : DataMigration
{
    /// <summary>
    /// Creates the initial schema.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CheckoutSessionIndex>(table => table
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("ReferenceType")
            .Column<string>("ReferenceId")
            .Column<string>("ReferenceVersionId")
            .Column<string>("OwnerId")
            .Column<CheckoutSessionStatus>("Status")
            .Column<DateTime>("CreatedUtc")
            .Column<DateTime>("ModifiedUtc")
            .Column<DateTime>("CompletedUtc", column => column.Nullable())
        );

        await SchemaBuilder.AlterIndexTableAsync<CheckoutSessionIndex>(table => table
            .CreateIndex("IDX_CheckoutSessionIndex_SessionId", "SessionId", "Status", "OwnerId")
        );

        await SchemaBuilder.AlterIndexTableAsync<CheckoutSessionIndex>(table => table
            .CreateIndex("IDX_CheckoutSessionIndex_Reference", "ReferenceType", "ReferenceId")
        );

        await SchemaBuilder.CreateMapIndexTableAsync<PaymentAttemptIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("ProviderKey")
            .Column<string>("ObligationId")
            .Column<string>("IdempotencyKey")
            .Column<string>("ProviderReference")
            .Column<PaymentAttemptState>("State")
            .Column<DateTime>("UpdatedUtc")
        );

        await SchemaBuilder.AlterIndexTableAsync<PaymentAttemptIndex>(table => table
            .CreateIndex("IDX_PaymentAttemptIndex_ItemId", "ItemId")
        );

        await SchemaBuilder.AlterIndexTableAsync<PaymentAttemptIndex>(table => table
            .CreateIndex("IDX_PaymentAttemptIndex_Session", "SessionId", "State")
        );

        await SchemaBuilder.AlterIndexTableAsync<PaymentAttemptIndex>(table => table
            .CreateIndex("IDX_PaymentAttemptIndex_Idempotency", "IdempotencyKey")
        );

        await SchemaBuilder.CreateMapIndexTableAsync<PaymentRefundIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("ProviderKey")
            .Column<string>("OriginalTransactionId")
            .Column<string>("ProviderRefundReference")
            .Column<string>("IdempotencyKey")
            .Column<RefundStatus>("Status")
            .Column<DateTime>("UpdatedUtc")
        );

        await SchemaBuilder.AlterIndexTableAsync<PaymentRefundIndex>(table => table
            .CreateIndex("IDX_PaymentRefundIndex_ItemId", "ItemId")
        );

        await SchemaBuilder.AlterIndexTableAsync<PaymentRefundIndex>(table => table
            .CreateIndex("IDX_PaymentRefundIndex_OriginalTransaction", "OriginalTransactionId", "Status")
        );

        await SchemaBuilder.AlterIndexTableAsync<PaymentRefundIndex>(table => table
            .CreateIndex("IDX_PaymentRefundIndex_Session", "SessionId")
        );

        await SchemaBuilder.AlterIndexTableAsync<PaymentRefundIndex>(table => table
            .CreateIndex("IDX_PaymentRefundIndex_Idempotency", "IdempotencyKey")
        );

        return 1;
    }
}
