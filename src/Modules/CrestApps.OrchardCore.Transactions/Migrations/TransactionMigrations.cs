using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Data.Migration;
using OrchardCore.Environment.Shell.Scope;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Transactions.Migrations;

/// <summary>
/// Creates the index table that backs the provider-agnostic transaction ledger.
/// </summary>
internal sealed class TransactionMigrations : DataMigration
{
    /// <summary>
    /// Creates the initial schema.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<TransactionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Title")
            .Column<string>("Source")
            .Column<string>("OwnerId", column => column.WithLength(26))
            .Column<int>("OwnerKind")
            .Column<string>("ReferenceType")
            .Column<string>("ReferenceId", column => column.WithLength(26))
            .Column<string>("ParentReferenceType", column => column.Nullable().WithLength(TransactionIndexProvider.MaxParentReferenceLength))
            .Column<string>("ParentReferenceId", column => column.Nullable().WithLength(TransactionIndexProvider.MaxParentReferenceLength))
            .Column<string>("CheckoutSessionId", column => column.WithLength(26))
            .Column<string>("ObligationId")
            .Column<string>("Currency", column => column.WithLength(8))
            .Column<decimal>("TotalAmount")
            .Column<decimal>("AmountPaid")
            .Column<decimal>("OutstandingAmount")
            .Column<TransactionStatus>("Status")
            .Column<DateTime>("DueUtc", column => column.Nullable())
            .Column<DateTime>("NextCycleUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc")
            .Column<DateTime>("UpdatedUtc"),
            collection: TransactionsConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<TransactionIndex>(table =>
        {
            table.CreateIndex("IDX_TransactionIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_TransactionIndex_Owner", "OwnerId", "Status");
            table.CreateIndex("IDX_TransactionIndex_Status", "Status", "DueUtc");
            table.CreateIndex("IDX_TransactionIndex_Obligation", "CheckoutSessionId", "ObligationId");
            table.CreateIndex("IDX_TransactionIndex_Reference", "ReferenceType", "ReferenceId");
            table.CreateIndex("IDX_TransactionIndex_ParentReference", "ParentReferenceType", "ParentReferenceId");
            table.CreateIndex("IDX_TransactionIndex_Outstanding", "Status", "OutstandingAmount");
            table.CreateIndex("IDX_TransactionIndex_NextCycle", "NextCycleUtc");
        }, collection: TransactionsConstants.CollectionName);

        await CreateReportIndexAsync();
        await CreatePayerColumnsAsync();

        await CreatePendingCreditColumnAsync();
        await CreateAutoChargeColumnAsync();
        await CreatePartyCollectionSettingsIndexAsync();

        return 6;
    }

    /// <summary>
    /// Adds the index reports read transactions through, newest first by <c>CreatedUtc</c> a page at a time, so each
    /// page and each date range is found by seeking.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await CreateReportIndexAsync();

        return 2;
    }

    /// <summary>
    /// Adds the payer columns, which say which party owes each transaction, and the index a party's transactions are
    /// found through.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await CreatePayerColumnsAsync();

        // The payer of a transaction recorded before these columns follows from its owner, so mapping each transaction
        // again fills them. It runs after the migration, in pages, so the migration itself only changes the schema.
        ShellScope.AddDeferredTask(scope => CatalogIndexBackfill.ReindexAsync<Transaction, TransactionIndex>(
            scope.ServiceProvider.GetRequiredService<ISession>(),
            TransactionsConstants.CollectionName));

        return 3;
    }

    /// <summary>
    /// Adds the column that marks a transaction whose timeline entry waits for its customer credit change, so the sweep
    /// finds the ones to complete. No existing transaction waits for one, so nothing is filled.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom3Async()
    {
        await CreatePendingCreditColumnAsync();

        return 4;
    }

    /// <summary>
    /// Adds the column the saved payment method charge sweep finds the scheduled charges through. No existing
    /// transaction has one scheduled, so nothing is filled.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom4Async()
    {
        await CreateAutoChargeColumnAsync();

        return 5;
    }

    /// <summary>
    /// Adds the index of the parties' collection settings.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom5Async()
    {
        await CreatePartyCollectionSettingsIndexAsync();

        return 6;
    }

    private async Task CreatePartyCollectionSettingsIndexAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<PartyCollectionSettingsIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("PartyKind", column => column.Nullable().WithLength(50))
            .Column<string>("PartyId", column => column.Nullable().WithLength(255))
            .Column<bool>("RemindersPaused")
            .Column<bool>("LateFeesExempt"),
            collection: TransactionsConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<PartyCollectionSettingsIndex>(table =>
        {
            table.CreateIndex("IDX_PartyCollectionSettingsIndex_Party", "PartyKind", "PartyId");
            table.CreateIndex("IDX_PartyCollectionSettingsIndex_Excluded", "RemindersPaused", "LateFeesExempt");
        }, collection: TransactionsConstants.CollectionName);
    }

    private async Task CreateAutoChargeColumnAsync()
    {
        await SchemaBuilder.AlterIndexTableAsync<TransactionIndex>(
            table => table.AddColumn<DateTime>("AutoChargeNextAttemptUtc", column => column.Nullable()),
            collection: TransactionsConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<TransactionIndex>(
            table => table.CreateIndex("IDX_TransactionIndex_AutoCharge", "AutoChargeNextAttemptUtc"),
            collection: TransactionsConstants.CollectionName);
    }

    private async Task CreatePendingCreditColumnAsync()
    {
        await SchemaBuilder.AlterIndexTableAsync<TransactionIndex>(
            table => table.AddColumn<bool>("PendingCredit", column => column.Nullable()),
            collection: TransactionsConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<TransactionIndex>(
            table => table.CreateIndex("IDX_TransactionIndex_PendingCredit", "PendingCredit"),
            collection: TransactionsConstants.CollectionName);
    }

    private async Task CreatePayerColumnsAsync()
    {
        await SchemaBuilder.AlterIndexTableAsync<TransactionIndex>(table =>
        {
            table.AddColumn<string>("PayerKind", column => column.Nullable().WithLength(26));
            table.AddColumn<string>("PayerId", column => column.Nullable().WithLength(TransactionIndexProvider.MaxParentReferenceLength));
        }, collection: TransactionsConstants.CollectionName);

        await IndexTableIndexCreation.CreateIfMissingAsync<TransactionIndex>(
            SchemaBuilder,
            "IDX_TransactionIndex_Payer",
            TransactionsConstants.CollectionName,
            "PayerKind", "PayerId", "Status");
    }

    private Task<bool> CreateReportIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<TransactionIndex>(
            SchemaBuilder,
            "IDX_TransactionIndex_Report",
            TransactionsConstants.CollectionName,
            "CreatedUtc", "Id");
    }
}
