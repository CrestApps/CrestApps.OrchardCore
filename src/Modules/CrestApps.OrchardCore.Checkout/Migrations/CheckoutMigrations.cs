using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Indexes;
using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Checkout.Migrations;

/// <summary>
/// Creates the index tables that back checkout sessions, the lookup of a person's checkout sessions, the durable
/// payment attempt ledger, the durable refund ledger, and the payment methods customers kept.
/// </summary>
internal sealed class CheckoutMigrations : DataMigration
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

        await SchemaBuilder.AlterIndexTableAsync<CheckoutSessionIndex>(table =>
        {
            table.CreateIndex("IDX_CheckoutSessionIndex_SessionId", "SessionId", "Status", "OwnerId");
            table.CreateIndex("IDX_CheckoutSessionIndex_Reference", "ReferenceType", "ReferenceId");
        });

        // Finds a person's checkouts by owner and by email, for access and erasure requests.
        await SchemaBuilder.CreateMapIndexTableAsync<CheckoutSessionCustomerIndex>(table => table
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("OwnerId")
            .Column<string>("NormalizedEmail", column => column.WithLength(CheckoutSessionCustomerIndexProvider.EmailLength))
            .Column<DateTime>("CreatedUtc")
        );

        await SchemaBuilder.AlterIndexTableAsync<CheckoutSessionCustomerIndex>(table =>
        {
            table.CreateIndex("IDX_CheckoutSessionCustomerIndex_Owner", "OwnerId", "CreatedUtc");
            table.CreateIndex("IDX_CheckoutSessionCustomerIndex_Email", "NormalizedEmail", "CreatedUtc");
        });

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

        // Reporting ranges over when money was taken and groups it by what it was taken for.
        await SchemaBuilder.AlterIndexTableAsync<PaymentAttemptIndex>(table =>
        {
            table.CreateIndex("IDX_PaymentAttemptIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_PaymentAttemptIndex_Session", "SessionId", "State");
            table.CreateIndex("IDX_PaymentAttemptIndex_Idempotency", "IdempotencyKey");
            table.CreateIndex("IDX_PaymentAttemptIndex_Reporting", "ReferenceType", "State", "CreatedUtc");
        });

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

        await SchemaBuilder.AlterIndexTableAsync<PaymentRefundIndex>(table =>
        {
            table.CreateIndex("IDX_PaymentRefundIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_PaymentRefundIndex_OriginalTransaction", "OriginalTransactionId", "Status");
            table.CreateIndex("IDX_PaymentRefundIndex_Session", "SessionId");
            table.CreateIndex("IDX_PaymentRefundIndex_Idempotency", "IdempotencyKey");
        });

        // The payment methods customers kept for charges made without them: listed per owner, and found again by the
        // provider's reference so keeping the same card twice updates it.
        await SchemaBuilder.CreateMapIndexTableAsync<CustomerPaymentMethodIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("OwnerId", column => column.WithLength(64))
            .Column<string>("ProviderKey", column => column.WithLength(64))
            .Column<string>("PaymentMethodReference", column => column.WithLength(255))
            .Column<bool>("IsDefault")
            .Column<DateTime>("CreatedUtc")
        );

        await SchemaBuilder.AlterIndexTableAsync<CustomerPaymentMethodIndex>(table =>
        {
            table.CreateIndex("IDX_CustomerPaymentMethodIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_CustomerPaymentMethodIndex_Owner", "OwnerId", "CreatedUtc");
            table.CreateIndex("IDX_CustomerPaymentMethodIndex_Reference", "OwnerId", "ProviderKey", "PaymentMethodReference");
        });

        await CreateReportIndexesAsync();

        return 2;
    }

    /// <summary>
    /// Adds the indexes reports read checkout sessions, payment attempts and kept payment methods through, newest first
    /// by <c>CreatedUtc</c> a page at a time, so each page and each date range is found by seeking.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await CreateReportIndexesAsync();

        return 2;
    }

    private async Task CreateReportIndexesAsync()
    {
        await IndexTableIndexCreation.CreateIfMissingAsync<CheckoutSessionIndex>(
            SchemaBuilder,
            "IDX_CheckoutSessionIndex_Report",
            null,
            "CreatedUtc", "Id");

        await IndexTableIndexCreation.CreateIfMissingAsync<PaymentAttemptIndex>(
            SchemaBuilder,
            "IDX_PaymentAttemptIndex_Report",
            null,
            "CreatedUtc", "Id");

        await IndexTableIndexCreation.CreateIfMissingAsync<CustomerPaymentMethodIndex>(
            SchemaBuilder,
            "IDX_CustomerPaymentMethodIndex_Report",
            null,
            "CreatedUtc", "Id");
    }
}
