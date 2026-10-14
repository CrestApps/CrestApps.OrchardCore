using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Models;
using OrchardCore.Data.Migration;
using YesSql.Sql;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Checkout.Core.Indexes;

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
            .Column<DateTime>("CreatedUtc")
            .Column<string>("Currency", column => column.WithLength(8))
            .Column<decimal>("ConfirmedAmount")
            .Column<decimal>("ConfirmedTaxAmount")
            .Column<string>("ReferenceType")
            .Column<string>("ReferenceId", column => column.WithLength(26))
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

        // Reporting ranges over when money was taken and groups it by what it was taken for.
        await SchemaBuilder.AlterIndexTableAsync<PaymentAttemptIndex>(table => table
            .CreateIndex("IDX_PaymentAttemptIndex_Reporting", "ReferenceType", "State", "CreatedUtc")
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

        return 2;
    }

    /// <summary>
    /// Adds the columns that let a report be built from the payment ledger rather than from a checkout
    /// session, and the index that makes ranging over them cheap.
    /// </summary>
    /// <returns>The schema version this migration upgrades to.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<PaymentAttemptIndex>(table =>
        {
            table.AddColumn<DateTime>("CreatedUtc");
            table.AddColumn<string>("Currency", column => column.WithLength(8));
            table.AddColumn<decimal>("ConfirmedAmount");
            table.AddColumn<decimal>("ConfirmedTaxAmount");
            table.AddColumn<string>("ReferenceType");
            table.AddColumn<string>("ReferenceId", column => column.WithLength(26));
        });

        await SchemaBuilder.AlterIndexTableAsync<PaymentAttemptIndex>(table => table
            .CreateIndex("IDX_PaymentAttemptIndex_Reporting", "ReferenceType", "State", "CreatedUtc")
        );

        return 2;
    }
}
