using System.Data.Common;
using System.Text;

namespace CrestApps.OrchardCore.Tests.Migrations;

/// <summary>
/// Reads the physical shape of a SQLite table so two independently produced schemas can be compared as text.
/// </summary>
/// <remarks>
/// A schema divergence between a freshly created tenant and an upgraded one cannot be seen by any functional
/// test, because both tenants answer every query correctly on SQLite: its column affinity silently reconciles
/// an integer written into a text column. The divergence only becomes a failure on an engine that does not
/// coerce, which is the engine production runs on. Comparing the declared shape is therefore the only evidence
/// available before deployment, so the shape is read from the database itself rather than inferred from the
/// migration source.
/// </remarks>
internal static class SqliteSchemaSnapshot
{
    /// <summary>
    /// Captures the columns and indexes of the specified table as a stable, comparable string.
    /// </summary>
    /// <param name="connection">The open connection to read the schema through.</param>
    /// <param name="transaction">The transaction the read participates in.</param>
    /// <param name="tableName">The unquoted table name.</param>
    /// <returns>A deterministic textual description of the table shape.</returns>
    public static async Task<string> CaptureAsync(
        DbConnection connection,
        DbTransaction transaction,
        string tableName)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrEmpty(tableName);

        var builder = new StringBuilder();

        builder.AppendLine("columns:");

        foreach (var column in (await ReadColumnsAsync(connection, transaction, tableName)).OrderBy(x => x, StringComparer.Ordinal))
        {
            builder.Append("  ").AppendLine(column);
        }

        builder.AppendLine("indexes:");

        foreach (var index in (await ReadIndexesAsync(connection, transaction, tableName)).OrderBy(x => x, StringComparer.Ordinal))
        {
            builder.Append("  ").AppendLine(index);
        }

        return builder.ToString();
    }

    private static async Task<List<string>> ReadColumnsAsync(
        DbConnection connection,
        DbTransaction transaction,
        string tableName)
    {
        var columns = new List<string>();

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA table_info('{tableName.Replace("'", "''", StringComparison.Ordinal)}')";

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var name = reader.GetString(1);
            var type = reader.GetString(2).ToUpperInvariant();
            var notNull = reader.GetInt32(3) == 1;
            var defaultValue = reader.IsDBNull(4)
                ? "<none>"
                : reader.GetString(4);

            columns.Add($"{name} type={type} notnull={notNull} default={defaultValue}");
        }

        return columns;
    }

    private static async Task<List<string>> ReadIndexesAsync(
        DbConnection connection,
        DbTransaction transaction,
        string tableName)
    {
        var indexes = new List<(string Name, bool IsUnique)>();

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = $"PRAGMA index_list('{tableName.Replace("'", "''", StringComparison.Ordinal)}')";

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var name = reader.GetString(1);

                // Indexes SQLite creates for a UNIQUE column constraint are named by the engine and carry no
                // migration intent, so they would report a difference that no migration can control.
                if (name.StartsWith("sqlite_autoindex", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                indexes.Add((name, reader.GetInt32(2) == 1));
            }
        }

        var described = new List<string>();

        foreach (var (name, isUnique) in indexes)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"PRAGMA index_info('{name.Replace("'", "''", StringComparison.Ordinal)}')";

            var indexColumns = new List<string>();

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                indexColumns.Add(reader.IsDBNull(2) ? "<expression>" : reader.GetString(2));
            }

            described.Add($"{name} unique={isUnique} columns=({string.Join(", ", indexColumns)})");
        }

        return described;
    }
}
