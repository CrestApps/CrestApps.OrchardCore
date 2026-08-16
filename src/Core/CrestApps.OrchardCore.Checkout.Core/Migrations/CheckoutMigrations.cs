using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Models;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Checkout.Core.Migrations;

/// <summary>
/// Creates the index tables that back checkout sessions and the durable payment attempt ledger.
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
            .Column<string>("AttemptId", column => column.WithLength(26))
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("ProviderKey")
            .Column<string>("ObligationId")
            .Column<string>("IdempotencyKey")
            .Column<string>("ProviderReference")
            .Column<PaymentAttemptState>("State")
            .Column<DateTime>("UpdatedUtc")
        );

        await SchemaBuilder.AlterIndexTableAsync<PaymentAttemptIndex>(table => table
            .CreateIndex("IDX_PaymentAttemptIndex_AttemptId", "AttemptId")
        );

        await SchemaBuilder.AlterIndexTableAsync<PaymentAttemptIndex>(table => table
            .CreateIndex("IDX_PaymentAttemptIndex_Session", "SessionId", "State")
        );

        await SchemaBuilder.AlterIndexTableAsync<PaymentAttemptIndex>(table => table
            .CreateIndex("IDX_PaymentAttemptIndex_Idempotency", "IdempotencyKey")
        );

        await CreatePaymentRefundSchemaAsync();

        return 2;
    }

    /// <summary>
    /// Adds the durable refund ledger schema for tenants created before it existed.
    /// </summary>
    public async Task<int> UpdateFrom1Async()
    {
        await CreatePaymentRefundSchemaAsync();

        return 2;
    }

    private async Task CreatePaymentRefundSchemaAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<PaymentRefundIndex>(table => table
            .Column<string>("RefundId", column => column.WithLength(26))
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("ProviderKey")
            .Column<string>("OriginalTransactionId")
            .Column<string>("ProviderRefundReference")
            .Column<string>("IdempotencyKey")
            .Column<RefundStatus>("Status")
            .Column<DateTime>("UpdatedUtc")
        );

        await SchemaBuilder.AlterIndexTableAsync<PaymentRefundIndex>(table => table
            .CreateIndex("IDX_PaymentRefundIndex_RefundId", "RefundId")
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
    }
}
