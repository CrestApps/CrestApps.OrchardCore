using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Migrations;

internal sealed class OmnichannelContactCommunicationPreferenceIndexMigrations : DataMigration
{
    private const string SqlServerDialectName = "SqlServer";

    private readonly IStore _store;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactCommunicationPreferenceIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    /// <param name="logger">The logger.</param>
    public OmnichannelContactCommunicationPreferenceIndexMigrations(
        IStore store,
        ILogger<OmnichannelContactCommunicationPreferenceIndexMigrations> logger)
    {
        _store = store;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new async.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        await EnsureDefaultContactCommunicationPreferenceIndexTableAsync();

        return 3;
    }

    /// <summary>
    /// Updates the from1 async.
    /// </summary>
    public async Task<int> UpdateFrom1Async()
    {
        await EnsureDefaultContactCommunicationPreferenceIndexTableAsync();

        return 2;
    }

    /// <summary>
    /// Removes the chat preference columns, which no channel could ever act on.
    /// </summary>
    public async Task<int> UpdateFrom2Async()
    {
        // The chat preference was writable but never readable: the platform has no chat channel, no processor for
        // one, and no path that can create chat work, so a contact who asked not to be chatted with was told
        // something the product could not honour. The promise is withdrawn, so the columns behind it go too.
        //
        // The columns are looked for rather than dropped blindly. The host runs this step on the transaction every
        // sibling step in the feature shares, and a drop that failed there would take all of them down with it, so a
        // table that never had these columns has to be recognised before anything is attempted. The check and the
        // drops run on that same transaction: a second connection would wait on SQLite for the write lock this
        // transaction already holds, stall every startup for the full busy timeout, and fail.
        //
        // SQL Server refuses to drop a column while a default constraint still references it, and DoNotChat was
        // declared with a default, so the constraint SQL Server generated for it comes down first. Without that, the
        // drop throws, the host cancels the session every migration shares, and every other feature's pending
        // migration is rolled back with it on each start.
        var columns = await GetColumnNamesAsync();

        if (columns.Contains("DoNotChat"))
        {
            await DropSqlServerColumnDefaultAsync("DoNotChat");

            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactCommunicationPreferenceIndex>(table =>
                table.DropColumn("DoNotChat"));
        }

        if (columns.Contains("DoNotChatUtc"))
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactCommunicationPreferenceIndex>(table =>
                table.DropColumn("DoNotChatUtc"));
        }

        return 3;
    }

    // The constraint is engine-generated, so its name is read from the catalog rather than assumed. The other engines
    // drop a column's default together with the column, so nothing runs there.
    private async Task DropSqlServerColumnDefaultAsync(string columnName)
    {
        if (!string.Equals(SchemaBuilder.Dialect.Name, SqlServerDialectName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var quotedTableName = GetQuotedTableName();
        var constraintName = await ReadSqlServerColumnDefaultNameAsync(quotedTableName, columnName);

        if (constraintName is null)
        {
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Dropping the default constraint '{ConstraintName}' on column '{ColumnName}' of '{TableName}' so the column can be dropped.",
                constraintName,
                columnName,
                quotedTableName);
        }

        await using var command = SchemaBuilder.Connection.CreateCommand();
        command.Transaction = SchemaBuilder.Transaction;
        command.CommandText = "alter table " + quotedTableName + " drop constraint " + SchemaBuilder.Dialect.QuoteForColumnName(constraintName);

        await command.ExecuteNonQueryAsync();
    }

    private async Task<string> ReadSqlServerColumnDefaultNameAsync(string quotedTableName, string columnName)
    {
        await using var command = SchemaBuilder.Connection.CreateCommand();
        command.Transaction = SchemaBuilder.Transaction;
        command.CommandText =
            "SELECT dc.name FROM sys.default_constraints dc " +
            "INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id " +
            "WHERE dc.parent_object_id = OBJECT_ID(@table) AND c.name = @column";

        var tableParameter = command.CreateParameter();
        tableParameter.ParameterName = "@table";
        tableParameter.Value = quotedTableName;
        command.Parameters.Add(tableParameter);

        var columnParameter = command.CreateParameter();
        columnParameter.ParameterName = "@column";
        columnParameter.Value = columnName;
        command.Parameters.Add(columnParameter);

        return await command.ExecuteScalarAsync() as string;
    }

    private string GetQuotedTableName()
    {
        var tableName = SchemaBuilder.TablePrefix +
            SchemaBuilder.TableNameConvention.GetIndexTable(typeof(OmnichannelContactCommunicationPreferenceIndex), null);

        return SchemaBuilder.Dialect.QuoteForTableName(tableName, _store.Configuration.Schema);
    }

    // Columns are read through the data reader rather than an engine-specific catalog view, so the same probe works
    // on every supported engine, and the query matches no rows because only the declared columns are wanted.
    private async Task<HashSet<string>> GetColumnNamesAsync()
    {
        var quotedTableName = GetQuotedTableName();

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

    private async Task EnsureDefaultContactCommunicationPreferenceIndexTableAsync()
    {
        try
        {
            await SchemaBuilder.CreateMapIndexTableAsync<OmnichannelContactCommunicationPreferenceIndex>(table => table
                .Column<string>("ContentItemId", column => column.WithLength(26))
                .Column<bool>("DoNotCall", column => column.NotNull().WithDefault(false))
                .Column<DateTime>("DoNotCallUtc")
                .Column<bool>("DoNotSms", column => column.NotNull().WithDefault(false))
                .Column<DateTime>("DoNotSmsUtc")
                .Column<bool>("DoNotEmail", column => column.NotNull().WithDefault(false))
                .Column<DateTime>("DoNotEmailUtc")
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The default-collection OmnichannelContactCommunicationPreferenceIndex table may already exist.");
        }

        try
        {
            await SchemaBuilder.AlterIndexTableAsync<OmnichannelContactCommunicationPreferenceIndex>(table => table
                .CreateIndex("IDX_OmnichannelContactCommunicationPreferenceIndex_DoNotCallUtc",
                    "DocumentId",
                    "DoNotCallUtc"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "The 'IDX_OmnichannelContactCommunicationPreferenceIndex_DoNotCallUtc' index may already exist on the default-collection OmnichannelContactCommunicationPreferenceIndex table.");
        }
    }
}
