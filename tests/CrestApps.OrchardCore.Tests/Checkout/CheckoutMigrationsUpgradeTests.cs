using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Core.Migrations;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Tests.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Transactions.Models;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// Requires a tenant created on an earlier shape of the payment ledger to end up with the ledger schema a tenant
/// created today has, and with its ledger documents indexed.
/// </summary>
/// <remarks>
/// The rename of the ledger's business id to ItemId and the refund ledger were folded into the create step instead of
/// being given an upgrade step, so a tenant created before them recorded version 2 with neither, and the payments and
/// refunds admin pages failed there on a missing column and a missing table.
/// </remarks>
public sealed class CheckoutMigrationsUpgradeTests
{
    // The superseded business id columns, and their indexes, stay on an upgraded tenant because the migrations only
    // ever add to the schema. Nothing reads or writes them.
    private static readonly string[] _supersededShapeLines =
    [
        "AttemptId type=TEXT notnull=False default=<none>",
        "IDX_PaymentAttemptIndex_AttemptId unique=False columns=(AttemptId)",
        "RefundId type=TEXT notnull=False default=<none>",
        "IDX_PaymentRefundIndex_RefundId unique=False columns=(RefundId)",
    ];

    [Theory]
    [InlineData(LegacyLedger.AttemptIdWithoutRefunds, 1)]
    [InlineData(LegacyLedger.AttemptIdWithReportingWithoutRefunds, 2)]
    [InlineData(LegacyLedger.AttemptIdWithRefundId, 2)]
    [InlineData(LegacyLedger.Current, 2)]
    public async Task LedgerIndexes_FreshTenantAndTenantUpgradedFromAnEarlierShape_HaveTheSameShape(LegacyLedger legacy, int startingVersion)
    {
        var freshPath = DatabasePath("checkout-ledger-fresh");
        var upgradedPath = DatabasePath("checkout-ledger-upgraded");
        var freshStore = await CreateStoreAsync(freshPath, withLedgerIndexes: true);
        var upgradedStore = await CreateStoreAsync(upgradedPath, withLedgerIndexes: true);

        try
        {
            int freshVersion;
            int upgradedVersion;
            (string Attempts, string Refunds) freshShape;
            (string Attempts, string Refunds) upgradedShape;

            await using (var session = freshStore.CreateSession())
            {
                var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
                var schemaBuilder = new SchemaBuilder(freshStore.Configuration, transaction);

                freshVersion = await new CheckoutMigrations(session) { SchemaBuilder = schemaBuilder }.CreateAsync();
                freshShape = await CaptureLedgerShapeAsync(schemaBuilder, transaction);
            }

            await using (var session = upgradedStore.CreateSession())
            {
                var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
                var schemaBuilder = new SchemaBuilder(upgradedStore.Configuration, transaction);
                await CreateLegacyLedgerAsync(schemaBuilder, legacy);

                upgradedVersion = await MigrationChainRunner.RunUpgradeChainAsync(
                    new CheckoutMigrations(session) { SchemaBuilder = schemaBuilder },
                    startingVersion);
                upgradedShape = await CaptureLedgerShapeAsync(schemaBuilder, transaction);
            }

            Assert.Equal(freshVersion, upgradedVersion);
            AssertSameShape(freshShape.Attempts, upgradedShape.Attempts);
            AssertSameShape(freshShape.Refunds, upgradedShape.Refunds);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(freshStore, freshPath);
            TemporarySqliteDatabase.DisposeAndDelete(upgradedStore, upgradedPath);
        }
    }

    [Fact]
    public async Task UpdateFrom2Async_TenantWithAttemptIdAndNoRefundTable_IndexesTheLedgerDocuments()
    {
        var path = DatabasePath("checkout-ledger-documents");
        var legacyStore = await CreateStoreAsync(path, withLedgerIndexes: false);
        IStore upgradedStore = null;

        try
        {
            await using (var session = legacyStore.CreateSession())
            {
                var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
                await CreateLegacyLedgerAsync(new SchemaBuilder(legacyStore.Configuration, transaction), LegacyLedger.AttemptIdWithReportingWithoutRefunds);
                await transaction.CommitAsync(TestContext.Current.CancellationToken);
            }

            // Saved with no ledger index registered, as nothing could index them on this schema.
            await using (var session = legacyStore.CreateSession())
            {
                await session.SaveAsync(new PaymentAttempt
                {
                    ItemId = "attempt-1",
                    SessionId = "session-1",
                    State = PaymentAttemptState.Succeeded,
                    Currency = "USD",
                    ConfirmedAmount = 12.5m,
                }, cancellationToken: TestContext.Current.CancellationToken);

                await session.SaveAsync(new PaymentRefund
                {
                    ItemId = "refund-1",
                    SessionId = "session-1",
                    OriginalTransactionId = "txn-1",
                    Status = RefundStatus.Succeeded,
                }, cancellationToken: TestContext.Current.CancellationToken);

                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            legacyStore.Dispose();
            legacyStore = null;

            upgradedStore = await CreateStoreAsync(path, withLedgerIndexes: true);

            int version;

            await using (var session = upgradedStore.CreateSession())
            {
                var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
                var schemaBuilder = new SchemaBuilder(upgradedStore.Configuration, transaction);

                version = await MigrationChainRunner.RunUpgradeChainAsync(
                    new CheckoutMigrations(session) { SchemaBuilder = schemaBuilder },
                    2);

                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            Assert.Equal(3, version);

            await using (var session = upgradedStore.CreateSession())
            {
                var attempt = await session.Query<PaymentAttempt, PaymentAttemptIndex>(index => index.ItemId == "attempt-1")
                    .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
                var refund = await session.Query<PaymentRefund, PaymentRefundIndex>(index => index.ItemId == "refund-1")
                    .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
                var refundsForTransaction = await session.Query<PaymentRefund, PaymentRefundIndex>(index => index.OriginalTransactionId == "txn-1")
                    .CountAsync(TestContext.Current.CancellationToken);

                Assert.NotNull(attempt);
                Assert.Equal("session-1", attempt.SessionId);
                Assert.NotNull(refund);
                Assert.Equal(1, refundsForTransaction);
            }
        }
        finally
        {
            legacyStore?.Dispose();
            TemporarySqliteDatabase.DisposeAndDelete(upgradedStore, path);
        }
    }

    [Fact]
    public async Task UpdateFrom2Async_RunTwice_LeavesTheSchemaAsTheFirstRunLeftIt()
    {
        var path = DatabasePath("checkout-ledger-repeat");
        var store = await CreateStoreAsync(path, withLedgerIndexes: true);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateLegacyLedgerAsync(schemaBuilder, LegacyLedger.AttemptIdWithReportingWithoutRefunds);
            var migration = new CheckoutMigrations(session) { SchemaBuilder = schemaBuilder };

            // A second instance activating the same tenant can run the step again before the first records it.
            Assert.Equal(3, await migration.UpdateFrom2Async());
            var first = await CaptureLedgerShapeAsync(schemaBuilder, transaction);
            Assert.Equal(3, await migration.UpdateFrom2Async());
            var second = await CaptureLedgerShapeAsync(schemaBuilder, transaction);

            Assert.Equal(first, second);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    /// <summary>
    /// The shapes of the payment ledger that a tenant can have, each produced by a build that shipped to a site.
    /// </summary>
    public enum LegacyLedger
    {
        /// <summary>
        /// The first ledger: the attempt keyed by AttemptId, with no reporting columns and no refund table.
        /// </summary>
        AttemptIdWithoutRefunds,

        /// <summary>
        /// The first ledger after the reporting step ran on it: AttemptId with the reporting columns, still no
        /// refund table.
        /// </summary>
        AttemptIdWithReportingWithoutRefunds,

        /// <summary>
        /// The ledger that added refunds while both tables were still keyed by their own business id columns.
        /// </summary>
        AttemptIdWithRefundId,

        /// <summary>
        /// The ledger the previous create step produced, which is today's shape recorded at version 2.
        /// </summary>
        Current,
    }

    private static async Task CreateLegacyLedgerAsync(SchemaBuilder schemaBuilder, LegacyLedger legacy)
    {
        await schemaBuilder.CreateMapIndexTableAsync<CheckoutSessionIndex>(table => table
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

        var idColumn = legacy == LegacyLedger.Current ? "ItemId" : "AttemptId";
        var withReporting = legacy is LegacyLedger.AttemptIdWithReportingWithoutRefunds or LegacyLedger.Current;

        await schemaBuilder.CreateMapIndexTableAsync<PaymentAttemptIndex>(table =>
        {
            table
                .Column<string>(idColumn, column => column.WithLength(26))
                .Column<string>("SessionId", column => column.WithLength(26))
                .Column<string>("ProviderKey")
                .Column<string>("ObligationId")
                .Column<string>("IdempotencyKey")
                .Column<string>("ProviderReference")
                .Column<PaymentAttemptState>("State")
                .Column<DateTime>("UpdatedUtc");

            if (withReporting)
            {
                table
                    .Column<DateTime>("CreatedUtc")
                    .Column<string>("Currency", column => column.WithLength(8))
                    .Column<decimal>("ConfirmedAmount")
                    .Column<decimal>("ConfirmedTaxAmount")
                    .Column<string>("ReferenceType")
                    .Column<string>("ReferenceId", column => column.WithLength(26));
            }
        });

        await schemaBuilder.AlterIndexTableAsync<PaymentAttemptIndex>(table => table
            .CreateIndex($"IDX_PaymentAttemptIndex_{idColumn}", idColumn));
        await schemaBuilder.AlterIndexTableAsync<PaymentAttemptIndex>(table => table
            .CreateIndex("IDX_PaymentAttemptIndex_Session", "SessionId", "State"));
        await schemaBuilder.AlterIndexTableAsync<PaymentAttemptIndex>(table => table
            .CreateIndex("IDX_PaymentAttemptIndex_Idempotency", "IdempotencyKey"));

        if (withReporting)
        {
            await schemaBuilder.AlterIndexTableAsync<PaymentAttemptIndex>(table => table
                .CreateIndex("IDX_PaymentAttemptIndex_Reporting", "ReferenceType", "State", "CreatedUtc"));
        }

        if (legacy is LegacyLedger.AttemptIdWithoutRefunds or LegacyLedger.AttemptIdWithReportingWithoutRefunds)
        {
            return;
        }

        var refundIdColumn = legacy == LegacyLedger.Current ? "ItemId" : "RefundId";

        await schemaBuilder.CreateMapIndexTableAsync<PaymentRefundIndex>(table => table
            .Column<string>(refundIdColumn, column => column.WithLength(26))
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("ProviderKey")
            .Column<string>("OriginalTransactionId")
            .Column<string>("ProviderRefundReference")
            .Column<string>("IdempotencyKey")
            .Column<RefundStatus>("Status")
            .Column<DateTime>("UpdatedUtc"));

        await schemaBuilder.AlterIndexTableAsync<PaymentRefundIndex>(table => table
            .CreateIndex($"IDX_PaymentRefundIndex_{refundIdColumn}", refundIdColumn));
        await schemaBuilder.AlterIndexTableAsync<PaymentRefundIndex>(table => table
            .CreateIndex("IDX_PaymentRefundIndex_OriginalTransaction", "OriginalTransactionId", "Status"));
        await schemaBuilder.AlterIndexTableAsync<PaymentRefundIndex>(table => table
            .CreateIndex("IDX_PaymentRefundIndex_Session", "SessionId"));
        await schemaBuilder.AlterIndexTableAsync<PaymentRefundIndex>(table => table
            .CreateIndex("IDX_PaymentRefundIndex_Idempotency", "IdempotencyKey"));
    }

    private static async Task<(string Attempts, string Refunds)> CaptureLedgerShapeAsync(SchemaBuilder schemaBuilder, System.Data.Common.DbTransaction transaction)
    {
        var attempts = await SqliteSchemaSnapshot.CaptureAsync(schemaBuilder.Connection, transaction, GetIndexTableName<PaymentAttemptIndex>(schemaBuilder));
        var refunds = await SqliteSchemaSnapshot.CaptureAsync(schemaBuilder.Connection, transaction, GetIndexTableName<PaymentRefundIndex>(schemaBuilder));

        return (attempts, refunds);
    }

    private static string GetIndexTableName<TIndex>(SchemaBuilder schemaBuilder)
        => schemaBuilder.TablePrefix + schemaBuilder.TableNameConvention.GetIndexTable(typeof(TIndex), null);

    /// <summary>
    /// Fails with every differing line, ignoring only the superseded business id columns an upgraded tenant keeps.
    /// </summary>
    private static void AssertSameShape(string freshShape, string upgradedShape)
    {
        var fresh = freshShape.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var upgraded = upgradedShape.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Except(_supersededShapeLines, StringComparer.Ordinal);
        var onlyFresh = fresh.Except(upgraded, StringComparer.Ordinal).ToArray();
        var onlyUpgraded = upgraded.Except(fresh, StringComparer.Ordinal).ToArray();

        if (onlyFresh.Length == 0 && onlyUpgraded.Length == 0)
        {
            return;
        }

        Assert.Fail(
            $"""
            A freshly created tenant and an upgraded tenant do not have the same payment ledger shape.

            Only on a fresh tenant:
            {string.Join(Environment.NewLine, onlyFresh)}

            Only on an upgraded tenant:
            {string.Join(Environment.NewLine, onlyUpgraded)}
            """);
    }

    private static string DatabasePath(string prefix)
        => Path.Combine(Path.GetTempPath(), $"crestapps-{prefix}-{Guid.NewGuid():N}.db");

    private static async Task<IStore> CreateStoreAsync(string databasePath, bool withLedgerIndexes)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));

        if (withLedgerIndexes)
        {
            store.RegisterIndexes(new IIndexProvider[] { new PaymentAttemptIndexProvider(), new PaymentRefundIndexProvider() });
        }

        await store.InitializeAsync(TestContext.Current.CancellationToken);

        return store;
    }
}
