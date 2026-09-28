using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using Microsoft.Extensions.Logging;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Omnichannel.Migrations;

internal sealed class OmnichannelContactCommunicationPreferenceIndexMigrations : DataMigration
{
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
        var columns = await GetColumnNamesAsync();

        if (columns.Contains("DoNotChat"))
        {
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

    // Columns are read through the data reader rather than an engine-specific catalog view, so the same probe works
    // on every supported engine, and the query matches no rows because only the declared columns are wanted.
    private async Task<HashSet<string>> GetColumnNamesAsync()
    {
        var tableName = SchemaBuilder.TablePrefix +
            SchemaBuilder.TableNameConvention.GetIndexTable(typeof(OmnichannelContactCommunicationPreferenceIndex), null);
        var quotedTableName = SchemaBuilder.Dialect.QuoteForTableName(tableName, _store.Configuration.Schema);

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
