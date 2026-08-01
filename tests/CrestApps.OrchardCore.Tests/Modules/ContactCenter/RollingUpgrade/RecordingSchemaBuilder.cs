using System.Data.Common;
using YesSql;
using YesSql.Sql;
using YesSql.Sql.Schema;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.RollingUpgrade;

/// <summary>
/// An <see cref="ISchemaBuilder"/> that captures the schema a migration step declares, and optionally
/// forwards the step to a real builder so the same run also produces a real database.
/// </summary>
/// <remarks>
/// Capturing intent rather than reading the resulting database is what makes the previous version's schema
/// knowable. A create step and an update step declare their columns independently, so the only way to learn
/// what an update step contributes — and therefore what the schema looked like before it ran — is to observe
/// the declaration itself.
/// </remarks>
internal sealed class RecordingSchemaBuilder : ISchemaBuilder
{
    private readonly ISchemaBuilder _inner;
    private readonly ISchemaBuilder _context;
    private readonly ISqlDialect _dialect;
    private readonly ITableNameConvention _tableNameConvention;
    private readonly string _tablePrefix;
    private readonly Dictionary<string, RecordedTable> _tables;

    private bool _recordingUpdateStep;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingSchemaBuilder"/> class.
    /// </summary>
    /// <param name="configuration">The store configuration supplying the dialect and naming conventions.</param>
    /// <param name="inner">The builder to forward schema operations to, or <see langword="null"/> to capture without executing.</param>
    /// <param name="tables">The shared table map that accumulates the captured schema.</param>
    /// <param name="context">
    /// A builder supplying the live connection and transaction while schema operations are only captured. A
    /// migration step may perform data work before it declares its columns, so a capture pass without a
    /// connection would abort before recording anything and silently understate what the step adds.
    /// </param>
    public RecordingSchemaBuilder(
        IConfiguration configuration,
        ISchemaBuilder inner,
        Dictionary<string, RecordedTable> tables,
        ISchemaBuilder context = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(tables);

        _inner = inner;
        _context = context;
        _dialect = configuration.SqlDialect;
        _tableNameConvention = configuration.TableNameConvention;
        _tablePrefix = configuration.TablePrefix;
        _tables = tables;
    }

    /// <summary>
    /// Gets the tables captured so far, keyed by table name.
    /// </summary>
    public IReadOnlyDictionary<string, RecordedTable> Tables => _tables;

    /// <summary>
    /// Marks subsequent captures as belonging to an update step, so added columns are attributed separately
    /// from created columns.
    /// </summary>
    public void BeginUpdateStep()
    {
        _recordingUpdateStep = true;
    }

    public string TablePrefix => _inner?.TablePrefix ?? _context?.TablePrefix ?? _tablePrefix;

    public ISqlDialect Dialect => _inner?.Dialect ?? _context?.Dialect ?? _dialect;

    public DbConnection Connection => _inner?.Connection ?? _context?.Connection;

    public DbTransaction Transaction => _inner?.Transaction ?? _context?.Transaction;

    public ITableNameConvention TableNameConvention => _inner?.TableNameConvention ?? _context?.TableNameConvention ?? _tableNameConvention;

    public bool ThrowOnError => _inner?.ThrowOnError ?? _context?.ThrowOnError ?? true;

    public async Task CreateMapIndexTableAsync(Type indexType, Action<ICreateTableCommand> table, string collection)
    {
        RecordCreate(indexType, table, collection);

        if (_inner is not null)
        {
            await _inner.CreateMapIndexTableAsync(indexType, table, collection);
        }
    }

    public async Task CreateReduceIndexTableAsync(Type indexType, Action<ICreateTableCommand> table, string collection)
    {
        RecordCreate(indexType, table, collection);

        if (_inner is not null)
        {
            await _inner.CreateReduceIndexTableAsync(indexType, table, collection);
        }
    }

    public async Task AlterIndexTableAsync(Type indexType, Action<IAlterTableCommand> table, string collection)
    {
        RecordAlter(indexType, table, collection);

        if (_inner is not null)
        {
            await _inner.AlterIndexTableAsync(indexType, table, collection);
        }
    }

    public ISchemaBuilder CreateMapIndexTable(Type indexType, Action<ICreateTableCommand> table, string collection)
    {
        RecordCreate(indexType, table, collection);
        _inner?.CreateMapIndexTable(indexType, table, collection);

        return this;
    }

    public ISchemaBuilder CreateReduceIndexTable(Type indexType, Action<ICreateTableCommand> table, string collection)
    {
        RecordCreate(indexType, table, collection);
        _inner?.CreateReduceIndexTable(indexType, table, collection);

        return this;
    }

    public ISchemaBuilder AlterIndexTable(Type indexType, Action<IAlterTableCommand> table, string collection)
    {
        RecordAlter(indexType, table, collection);
        _inner?.AlterIndexTable(indexType, table, collection);

        return this;
    }

    public Task AlterTableAsync(string name, Action<IAlterTableCommand> table)
        => _inner is null ? Task.CompletedTask : _inner.AlterTableAsync(name, table);

    public Task CreateTableAsync(string name, Action<ICreateTableCommand> table)
        => _inner is null ? Task.CompletedTask : _inner.CreateTableAsync(name, table);

    public Task CreateForeignKeyAsync(string name, string srcTable, string[] srcColumns, string destTable, string[] destColumns)
        => _inner is null ? Task.CompletedTask : _inner.CreateForeignKeyAsync(name, srcTable, srcColumns, destTable, destColumns);

    public Task DropForeignKeyAsync(string srcTable, string name)
        => _inner is null ? Task.CompletedTask : _inner.DropForeignKeyAsync(srcTable, name);

    public Task DropMapIndexTableAsync(Type indexType, string collection)
        => _inner is null ? Task.CompletedTask : _inner.DropMapIndexTableAsync(indexType, collection);

    public Task DropReduceIndexTableAsync(Type indexType, string collection)
        => _inner is null ? Task.CompletedTask : _inner.DropReduceIndexTableAsync(indexType, collection);

    public Task DropTableAsync(string name)
        => _inner is null ? Task.CompletedTask : _inner.DropTableAsync(name);

    public Task CreateSchemaAsync(string schema)
        => _inner is null ? Task.CompletedTask : _inner.CreateSchemaAsync(schema);

    public ISchemaBuilder AlterTable(string name, Action<IAlterTableCommand> table)
    {
        _inner?.AlterTable(name, table);

        return this;
    }

    public ISchemaBuilder CreateTable(string name, Action<ICreateTableCommand> table)
    {
        _inner?.CreateTable(name, table);

        return this;
    }

    public ISchemaBuilder CreateForeignKey(string name, string srcTable, string[] srcColumns, string destTable, string[] destColumns)
    {
        _inner?.CreateForeignKey(name, srcTable, srcColumns, destTable, destColumns);

        return this;
    }

    public ISchemaBuilder DropForeignKey(string srcTable, string name)
    {
        _inner?.DropForeignKey(srcTable, name);

        return this;
    }

    public ISchemaBuilder DropMapIndexTable(Type indexType, string collection)
    {
        _inner?.DropMapIndexTable(indexType, collection);

        return this;
    }

    public ISchemaBuilder DropReduceIndexTable(Type indexType, string collection)
    {
        _inner?.DropReduceIndexTable(indexType, collection);

        return this;
    }

    public ISchemaBuilder DropTable(string name)
    {
        _inner?.DropTable(name);

        return this;
    }

    public ISchemaBuilder CreateSchema(string schema)
    {
        _inner?.CreateSchema(schema);

        return this;
    }

    private void RecordCreate(Type indexType, Action<ICreateTableCommand> table, string collection)
    {
        var recorded = GetOrAddTable(indexType, collection);
        var command = new CreateTableCommand(recorded.TableName);
        table(command);

        foreach (var column in command.TableCommands.OfType<CreateColumnCommand>())
        {
            recorded.CreatedColumns.Add(ToRecordedColumn(column.ColumnName, column));
        }
    }

    private void RecordAlter(Type indexType, Action<IAlterTableCommand> table, string collection)
    {
        var recorded = GetOrAddTable(indexType, collection);
        var command = new AlterTableCommand(recorded.TableName, Dialect, TablePrefix);
        table(command);

        foreach (var column in command.TableCommands.OfType<AddColumnCommand>())
        {
            // A column-widening rebuild adds a temporary replacement, copies into it, drops the original, and
            // renames the replacement back to the original name. On SQLite every text column is unbounded TEXT,
            // so widening changes nothing this recorder can observe, and the net effect on the schema is that the
            // original column keeps its name and identity. Recording the transient replacement would model a
            // table that still carries a column the finished rebuild has already renamed away, and every write
            // projected from that model would name a column the real table does not have. The drop and rename
            // that complete the swap are not recorded either — only added columns are — which leaves the original
            // column exactly as the create step declared it.
            if (IsRebuildTemporaryColumn(column.ColumnName))
            {
                continue;
            }

            var added = ToRecordedColumn(column.ColumnName, column);

            if (_recordingUpdateStep)
            {
                recorded.AddedColumns.Add(added);
            }
            else
            {
                recorded.CreatedColumns.Add(added);
            }
        }
    }

    // Mirrors the temporary-column suffixes the rebuild helpers append while widening or re-typing a column in
    // place. They are an implementation detail of the swap and never part of the finished schema.
    private static bool IsRebuildTemporaryColumn(string columnName)
    {
        return columnName.EndsWith("__widen", StringComparison.Ordinal)
            || columnName.EndsWith("__rebuild", StringComparison.Ordinal);
    }

    private RecordedTable GetOrAddTable(Type indexType, string collection)
    {
        var tableName = TableNameConvention.GetIndexTable(indexType, collection);

        if (!_tables.TryGetValue(tableName, out var recorded))
        {
            recorded = new RecordedTable(indexType, tableName);
            _tables[tableName] = recorded;
        }

        return recorded;
    }

    private static RecordedColumn ToRecordedColumn(string name, IColumnCommand column)
    {
        var isNotNull = column is CreateColumnCommand create
            ? create.IsNotNull
            : column is AddColumnCommand add && add.IsNotNull;

        var isUnique = column is CreateColumnCommand createUnique
            ? createUnique.IsUnique
            : column is AddColumnCommand addUnique && addUnique.IsUnique;

        return new RecordedColumn(name, column.DbType, column.Length, isNotNull, isUnique, column.Default);
    }
}
