using System.Data.Common;
using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Workforce.Models;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Workforce.Migrations;

/// <summary>
/// Moves the pay periods and pay codes the time clock kept in the Workforce collection to Pay Setup, which owns them now.
/// </summary>
/// <remarks>
/// Each record is read from its old document as JSON, because its old type no longer exists, and saved in the Payroll
/// collection as a <see cref="PaySchedule"/> or <see cref="PayCode"/> with the same id, so the policies, timesheets and
/// lines that name it still find it. A pay code's shift differential window goes into its
/// <see cref="WorkforcePayCodeSettings"/>, and its old kind number becomes the kind's name. A record already in the
/// Payroll collection with the same id is left as it is. A pay code Pay Setup seeded with the same code a moment ago is
/// replaced by the old one, which keeps its id. The old documents and index tables are then removed, so a second run
/// finds nothing to move. Everything runs on the migration's own connection and transaction.
/// </remarks>
internal static class TimeClockPaySetupMove
{
    /// <summary>
    /// The type name YesSql stored on each old pay period document.
    /// </summary>
    public const string LegacyPayPeriodTypeName = "CrestApps.OrchardCore.Workforce.Models.PayPeriodCalendar, CrestApps.OrchardCore.Workforce.Abstractions";

    /// <summary>
    /// The type name YesSql stored on each old pay code document.
    /// </summary>
    public const string LegacyPayCodeTypeName = "CrestApps.OrchardCore.Workforce.Models.PayCode, CrestApps.OrchardCore.Workforce.Abstractions";

    /// <summary>
    /// Moves the pay periods and pay codes.
    /// </summary>
    /// <param name="schemaBuilder">The migration's schema builder, whose connection and transaction the move uses.</param>
    /// <param name="store">The store, for its table naming and content serializer.</param>
    /// <param name="session">The migration's session, which saves the moved records.</param>
    /// <param name="dropLegacyTables">Whether the old index tables exist and are dropped.</param>
    /// <returns>The number of schedules and codes moved.</returns>
    public static async Task<(int Schedules, int Codes)> MoveAsync(ISchemaBuilder schemaBuilder, IStore store, ISession session, bool dropLegacyTables = true)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(session);

        var dialect = schemaBuilder.Dialect;
        var documentTable = dialect.QuoteForTableName(
            schemaBuilder.TablePrefix + schemaBuilder.TableNameConvention.GetDocumentTable(WorkforceConstants.CollectionName),
            store.Configuration.Schema);
        var serializer = store.Configuration.ContentSerializer;

        var schedules = 0;
        var existingSchedules = (await session.QueryIndex<PayScheduleIndex>(collection: PayrollConstants.CollectionName).ListAsync())
            .Select(index => index.ItemId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var content in await ReadAsync(schemaBuilder, documentTable, LegacyPayPeriodTypeName))
        {
            if (serializer.Deserialize(content, typeof(PaySchedule)) is not PaySchedule schedule ||
                string.IsNullOrEmpty(schedule.ItemId) ||
                !existingSchedules.Add(schedule.ItemId))
            {
                continue;
            }

            // The old calendar paid five days after the period, the defaults of a new schedule.
            await session.SaveAsync(schedule, false, PayrollConstants.CollectionName);
            schedules++;
        }

        var codes = 0;
        var existingCodes = await ReadCodeIndexAsync(schemaBuilder, store);
        var existingCodeIds = existingCodes.Select(index => index.ItemId).ToHashSet(StringComparer.Ordinal);

        foreach (var content in await ReadAsync(schemaBuilder, documentTable, LegacyPayCodeTypeName))
        {
            if (ToPayCode(content, serializer) is not { } code || string.IsNullOrEmpty(code.ItemId) || !existingCodeIds.Add(code.ItemId))
            {
                continue;
            }

            // Pay Setup seeds the usual codes when it is turned on, which on an existing site is just before this move.
            var seeded = existingCodes.Where(row => string.Equals(row.Code, code.Code, StringComparison.OrdinalIgnoreCase)).ToList();

            if (seeded.Count > 0)
            {
                foreach (var duplicate in await session.GetAsync<PayCode>([.. seeded.Select(row => row.DocumentId)], PayrollConstants.CollectionName))
                {
                    session.Delete(duplicate, PayrollConstants.CollectionName);
                }

                existingCodes.RemoveAll(seeded.Contains);
            }

            await session.SaveAsync(code, false, PayrollConstants.CollectionName);
            codes++;
        }

        if (dropLegacyTables)
        {
            await DropLegacyIndexTablesAsync(schemaBuilder);
        }

        await DeleteLegacyDocumentsAsync(schemaBuilder, documentTable);

        return (schedules, codes);
    }

    /// <summary>
    /// Reads an old pay code: its kind was a number or a name, and its window was on the code itself.
    /// </summary>
    /// <param name="content">The old document's JSON.</param>
    /// <param name="serializer">The store's content serializer.</param>
    /// <returns>The pay code, or <see langword="null"/>.</returns>
    internal static PayCode ToPayCode(string content, IContentSerializer serializer)
    {
        if (JsonNode.Parse(content) is not JsonObject json)
        {
            return null;
        }

        var kind = json["Kind"] is JsonValue kindValue && kindValue.TryGetValue<int>(out var number)
            ? (number >= 0 && number < PayCodeKinds.LegacyOrder.Count ? PayCodeKinds.LegacyOrder[number] : PayCodeKinds.Regular)
            : json["Kind"] is JsonValue named && named.TryGetValue<string>(out var name) && !string.IsNullOrWhiteSpace(name) ? name : PayCodeKinds.Regular;

        json["Kind"] = kind;

        var window = serializer.Deserialize(content, typeof(WorkforcePayCodeSettings)) as WorkforcePayCodeSettings;

        json.Remove("WindowStart");
        json.Remove("WindowEnd");
        json.Remove("Weekdays");

        if (serializer.Deserialize(json.ToJsonString(), typeof(PayCode)) is not PayCode code)
        {
            return null;
        }

        code.Class = kind == PayCodeKinds.Unpaid ? PayCodeClass.Memo : PayCodeClass.Earning;

        if (window is not null && (window.WindowStart.HasValue || window.WindowEnd.HasValue || window.Weekdays.Count > 0))
        {
            code.Put(window);
        }

        return code;
    }

    // The codes already in Pay Setup, with the document each index row belongs to, read on the migration's connection.
    private static async Task<List<(long DocumentId, string ItemId, string Code)>> ReadCodeIndexAsync(ISchemaBuilder schemaBuilder, IStore store)
    {
        var dialect = schemaBuilder.Dialect;
        var table = dialect.QuoteForTableName(
            schemaBuilder.TablePrefix + schemaBuilder.TableNameConvention.GetIndexTable(typeof(PayCodeIndex), PayrollConstants.CollectionName),
            store.Configuration.Schema);
        var rows = new List<(long DocumentId, string ItemId, string Code)>();

        await using var command = schemaBuilder.Connection.CreateCommand();

        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = $"SELECT {dialect.QuoteForColumnName("DocumentId")}, {dialect.QuoteForColumnName("ItemId")}, {dialect.QuoteForColumnName("Code")} FROM {table}";

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rows.Add((Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture), reader.IsDBNull(1) ? null : reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
        }

        return rows;
    }

    private static async Task<List<string>> ReadAsync(ISchemaBuilder schemaBuilder, string documentTable, string typeName)
    {
        var dialect = schemaBuilder.Dialect;
        var contents = new List<string>();

        await using var command = schemaBuilder.Connection.CreateCommand();

        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = $"SELECT {dialect.QuoteForColumnName("Content")} FROM {documentTable} WHERE {dialect.QuoteForColumnName("Type")} = @type ORDER BY {dialect.QuoteForColumnName("Id")}";
        AddParameter(command, "@type", typeName);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            contents.Add(reader.GetString(0));
        }

        return contents;
    }

    // The tables held the old documents' index rows; the Payroll collection has its own.
    private static async Task DropLegacyIndexTablesAsync(ISchemaBuilder schemaBuilder)
    {
        await schemaBuilder.DropTableAsync("Workforce_PayPeriodCalendarIndex");
        await schemaBuilder.DropTableAsync("Workforce_PayCodeIndex");
    }

    // Every old document has been saved again in the Payroll collection with the same id, so only the old copies go.
    private static async Task DeleteLegacyDocumentsAsync(ISchemaBuilder schemaBuilder, string documentTable)
    {
        await using var command = schemaBuilder.Connection.CreateCommand();

        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = $"DELETE FROM {documentTable} WHERE {schemaBuilder.Dialect.QuoteForColumnName("Type")} IN (@schedule, @code)";
        AddParameter(command, "@schedule", LegacyPayPeriodTypeName);
        AddParameter(command, "@code", LegacyPayCodeTypeName);

        await command.ExecuteNonQueryAsync();
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();

        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
