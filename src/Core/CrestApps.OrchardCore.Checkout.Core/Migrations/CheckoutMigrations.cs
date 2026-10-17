using System.Data.Common;
using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;
using YesSql.Sql.Schema;
using ISession = YesSql.ISession;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Checkout.Core.Indexes;

namespace CrestApps.OrchardCore.Checkout.Core.Migrations;

/// <summary>
/// Creates the index tables that back checkout sessions, the durable payment attempt ledger, and the
/// durable refund ledger.
/// </summary>
public sealed class CheckoutMigrations : DataMigration
{
    // The payment attempt columns a tenant can be missing although it records version 2. A tenant created before the
    // business id was renamed has AttemptId instead of ItemId, and one created before reporting existed has none of the
    // reporting columns; both were later folded into CreateAsync rather than given an upgrade step of their own.
    private static readonly (string Name, Action<IAlterTableCommand> Add)[] _paymentAttemptUpgradeColumns =
    [
        ("ItemId", table => table.AddColumn<string>("ItemId", column => column.WithLength(26))),
        ("CreatedUtc", table => table.AddColumn<DateTime>("CreatedUtc")),
        ("Currency", table => table.AddColumn<string>("Currency", column => column.WithLength(8))),
        ("ConfirmedAmount", table => table.AddColumn<decimal>("ConfirmedAmount")),
        ("ConfirmedTaxAmount", table => table.AddColumn<decimal>("ConfirmedTaxAmount")),
        ("ReferenceType", table => table.AddColumn<string>("ReferenceType")),
        ("ReferenceId", table => table.AddColumn<string>("ReferenceId", column => column.WithLength(26))),
    ];

    // A tenant whose refund table was created before the business id was renamed has RefundId instead of ItemId.
    private static readonly (string Name, Action<IAlterTableCommand> Add)[] _paymentRefundUpgradeColumns =
    [
        ("ItemId", table => table.AddColumn<string>("ItemId", column => column.WithLength(26))),
    ];

    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="CheckoutMigrations"/> class.
    /// </summary>
    /// <param name="session">
    /// The session the host runs the migration on. Documents are re-indexed through it so the work shares the
    /// migration's own transaction rather than opening a second connection.
    /// </param>
    public CheckoutMigrations(ISession session)
    {
        _session = session;
    }

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

        await CreatePaymentRefundIndexTableAsync();

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

        return 3;
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

    /// <summary>
    /// Brings a tenant that records version 2 but was created on an earlier shape of the ledger up to the schema a
    /// tenant created today has: the payment attempt index gains ItemId (and the reporting columns, when missing), the
    /// refund index table is created when it is not there, and the ledger documents are indexed again.
    /// </summary>
    /// <remarks>
    /// The rename of the business id to ItemId and the refund ledger were folded into <see cref="CreateAsync"/> instead
    /// of being given an upgrade step, so a tenant created before them reached version 2 with neither, and every query
    /// on the ledger failed there. Each change is made only when it is missing, because a tenant created at version 2
    /// already has all of them and must pass through this step untouched. Every probe runs on the migration's own
    /// transaction: a second connection would wait on SQLite for the write lock this transaction holds.
    /// <para>
    /// The superseded AttemptId and RefundId columns are left in place. Nothing writes or reads them any more, and the
    /// migrations here only ever add to the schema so that a node still running the previous build keeps working.
    /// </para>
    /// </remarks>
    /// <returns>The schema version this migration upgrades to.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        var attemptColumnsAdded = await AddMissingColumnsAsync<PaymentAttemptIndex>(_paymentAttemptUpgradeColumns);

        await IndexTableIndexCreation.CreateIfMissingAsync<PaymentAttemptIndex>(SchemaBuilder, "IDX_PaymentAttemptIndex_ItemId", null, "ItemId");
        await IndexTableIndexCreation.CreateIfMissingAsync<PaymentAttemptIndex>(SchemaBuilder, "IDX_PaymentAttemptIndex_Session", null, "SessionId", "State");
        await IndexTableIndexCreation.CreateIfMissingAsync<PaymentAttemptIndex>(SchemaBuilder, "IDX_PaymentAttemptIndex_Idempotency", null, "IdempotencyKey");
        await IndexTableIndexCreation.CreateIfMissingAsync<PaymentAttemptIndex>(SchemaBuilder, "IDX_PaymentAttemptIndex_Reporting", null, "ReferenceType", "State", "CreatedUtc");

        bool refundsNeedIndexing;

        if (await IndexTableExistsAsync<PaymentRefundIndex>())
        {
            refundsNeedIndexing = await AddMissingColumnsAsync<PaymentRefundIndex>(_paymentRefundUpgradeColumns);
        }
        else
        {
            await CreatePaymentRefundIndexTableAsync();
            refundsNeedIndexing = true;
        }

        await IndexTableIndexCreation.CreateIfMissingAsync<PaymentRefundIndex>(SchemaBuilder, "IDX_PaymentRefundIndex_ItemId", null, "ItemId");
        await IndexTableIndexCreation.CreateIfMissingAsync<PaymentRefundIndex>(SchemaBuilder, "IDX_PaymentRefundIndex_OriginalTransaction", null, "OriginalTransactionId", "Status");
        await IndexTableIndexCreation.CreateIfMissingAsync<PaymentRefundIndex>(SchemaBuilder, "IDX_PaymentRefundIndex_Session", null, "SessionId");
        await IndexTableIndexCreation.CreateIfMissingAsync<PaymentRefundIndex>(SchemaBuilder, "IDX_PaymentRefundIndex_Idempotency", null, "IdempotencyKey");

        // A document's index rows are written when it is saved, so the rows written before a column existed, and the
        // documents saved while the refund table was missing, are filled in by saving each document once more. The
        // ledger holds few documents, and saving them here keeps the work on the migration's transaction instead of
        // deferring it to a task that does not run when the migration runs at tenant start.
        if (attemptColumnsAdded)
        {
            await ReindexAsync<PaymentAttempt>();
        }

        if (refundsNeedIndexing)
        {
            await ReindexAsync<PaymentRefund>();
        }

        return 3;
    }

    private Task CreatePaymentRefundIndexTableAsync()
        => SchemaBuilder.CreateMapIndexTableAsync<PaymentRefundIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("ProviderKey")
            .Column<string>("OriginalTransactionId")
            .Column<string>("ProviderRefundReference")
            .Column<string>("IdempotencyKey")
            .Column<RefundStatus>("Status")
            .Column<DateTime>("UpdatedUtc")
        );

    private async Task<bool> AddMissingColumnsAsync<TIndex>((string Name, Action<IAlterTableCommand> Add)[] columns)
    {
        var existing = await GetIndexTableColumnNamesAsync<TIndex>();
        var missing = columns.Where(column => !existing.Contains(column.Name)).ToArray();

        if (missing.Length == 0)
        {
            return false;
        }

        await SchemaBuilder.AlterIndexTableAsync<TIndex>(table =>
        {
            foreach (var column in missing)
            {
                column.Add(table);
            }
        });

        return true;
    }

    private async Task ReindexAsync<T>()
        where T : class
    {
        foreach (var document in await _session.Query<T>().ListAsync())
        {
            await _session.SaveAsync(document);
        }
    }

    // Read from the catalog rather than by selecting from the table, because a statement against a missing table
    // aborts the shared migration transaction on PostgreSQL.
    private async Task<bool> IndexTableExistsAsync<TIndex>()
    {
        var configuration = _session.Store.Configuration;
        var tableName = GetIndexTableName<TIndex>();

        await using var command = SchemaBuilder.Connection.CreateCommand();
        command.Transaction = SchemaBuilder.Transaction;

        if (string.Equals(SchemaBuilder.Dialect.Name, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            command.CommandText = "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name = @TableName";
        }
        else if (string.IsNullOrEmpty(configuration.Schema))
        {
            command.CommandText = "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @TableName";
        }
        else
        {
            command.CommandText = "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @TableName";
            AddParameter(command, "@Schema", configuration.Schema);
        }

        AddParameter(command, "@TableName", tableName);

        return Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture) > 0;
    }

    // Columns are read through the data reader rather than an engine-specific catalog view, so the same probe works
    // on every supported engine, and the query matches no rows because only the declared columns are wanted.
    private async Task<HashSet<string>> GetIndexTableColumnNamesAsync<TIndex>()
    {
        var quotedTableName = SchemaBuilder.Dialect.QuoteForTableName(GetIndexTableName<TIndex>(), _session.Store.Configuration.Schema);

        await using var command = SchemaBuilder.Connection.CreateCommand();
        command.Transaction = SchemaBuilder.Transaction;
        command.CommandText = $"SELECT * FROM {quotedTableName} WHERE 1 = 0";

        await using var reader = await command.ExecuteReaderAsync();

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
        {
            columns.Add(reader.GetName(ordinal));
        }

        return columns;
    }

    private string GetIndexTableName<TIndex>()
        => SchemaBuilder.TablePrefix + SchemaBuilder.TableNameConvention.GetIndexTable(typeof(TIndex), null);

    private static void AddParameter(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
