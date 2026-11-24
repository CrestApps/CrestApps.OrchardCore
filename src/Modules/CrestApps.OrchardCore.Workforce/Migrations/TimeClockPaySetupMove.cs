using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Payroll;
using CrestApps.OrchardCore.Payroll.Core.Indexes;
using CrestApps.OrchardCore.Payroll.Core.Services;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.Logging;
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
/// Payroll collection with the same id is left as it is. A pay code Pay Setup seeded with the same code a moment ago, and
/// that nobody changed since, is replaced by the old one, which keeps its id; a Payroll code with the same code that a
/// person or a recipe made or changed stays, and the old one moves in beside it under a suffixed code, with a warning.
/// Only the old documents that were moved, or whose record was already in Payroll, are deleted, by their document id: one
/// that can't be read stays where it is and is logged, by its id and type, never its content. The old index tables are
/// dropped. A second run finds nothing to move. Everything runs on the migration's own connection and transaction.
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
    /// The suffix of an old pay code's code when a Payroll code someone made or changed already has its code.
    /// </summary>
    public const string RenamedSuffix = "-OLD";

    /// <summary>
    /// What a move did.
    /// </summary>
    /// <param name="Schedules">The pay periods moved.</param>
    /// <param name="Codes">The pay codes moved.</param>
    /// <param name="LeftInPlace">The old documents that couldn't be read and stay in the Workforce collection.</param>
    /// <param name="Renamed">The pay codes moved under a suffixed code, beside a Payroll code someone made or changed.</param>
    public sealed record MoveResult(int Schedules, int Codes, int LeftInPlace, int Renamed);

    /// <summary>
    /// Moves the pay periods and pay codes.
    /// </summary>
    /// <param name="schemaBuilder">The migration's schema builder, whose connection and transaction the move uses.</param>
    /// <param name="store">The store, for its table naming and content serializer.</param>
    /// <param name="session">The migration's session, which saves the moved records.</param>
    /// <param name="dropLegacyTables">Whether the old index tables exist and are dropped.</param>
    /// <param name="logger">Where each document left in place and each renamed code is reported, or <see langword="null"/>.</param>
    /// <returns>What the move did.</returns>
    public static async Task<MoveResult> MoveAsync(ISchemaBuilder schemaBuilder, IStore store, ISession session, bool dropLegacyTables = true, ILogger logger = null)
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
        var left = 0;
        var renamed = 0;
        var handled = new List<long>();

        void LeaveInPlace(long documentId, string typeName)
        {
            left++;

            if (logger?.IsEnabled(LogLevel.Warning) == true)
            {
                logger.LogWarning("The old document {DocumentId} of type {Type} couldn't be read as a pay period or pay code, so it was left in the Workforce collection.", documentId, typeName);
            }
        }

        var existingSchedules = (await session.QueryIndex<PayrollPayScheduleIndex>(collection: PayrollConstants.CollectionName).ListAsync())
            .Select(index => index.ItemId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var (documentId, content) in await ReadAsync(schemaBuilder, documentTable, LegacyPayPeriodTypeName))
        {
            if (TryRead(() => serializer.Deserialize(content, typeof(PaySchedule)) as PaySchedule) is not { } schedule || string.IsNullOrEmpty(schedule.ItemId))
            {
                LeaveInPlace(documentId, LegacyPayPeriodTypeName);

                continue;
            }

            // Its record is in Payroll already (moved before, or a copy with the same id): only the old copy goes.
            handled.Add(documentId);

            if (!existingSchedules.Add(schedule.ItemId))
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

        foreach (var (documentId, content) in await ReadAsync(schemaBuilder, documentTable, LegacyPayCodeTypeName))
        {
            if (TryRead(() => ToPayCode(content, serializer)) is not { } code || string.IsNullOrEmpty(code.ItemId))
            {
                LeaveInPlace(documentId, LegacyPayCodeTypeName);

                continue;
            }

            handled.Add(documentId);

            if (!existingCodeIds.Add(code.ItemId))
            {
                continue;
            }

            var sameCode = existingCodes.Where(row => string.Equals(row.Code, code.Code, StringComparison.OrdinalIgnoreCase)).ToList();

            if (sameCode.Count > 0)
            {
                var others = await session.GetAsync<PayCode>([.. sameCode.Select(row => row.DocumentId)], PayrollConstants.CollectionName);

                // Pay Setup seeds the usual codes when it is turned on, which on an existing site is just before this move:
                // such a code gives way. One a person or a recipe made or changed stays, and the old code takes another code.
                if (others.All(IsUntouchedSeed))
                {
                    foreach (var duplicate in others)
                    {
                        session.Delete(duplicate, PayrollConstants.CollectionName);
                    }

                    existingCodes.RemoveAll(sameCode.Contains);
                }
                else
                {
                    var original = code.Code;

                    code.Code = FreeCode(original, existingCodes);
                    renamed++;

                    if (logger?.IsEnabled(LogLevel.Warning) == true)
                    {
                        logger.LogWarning("Pay Setup already has a pay code {Code} that someone made or changed, so the time clock's pay code {ItemId} was moved as {NewCode}.", original, code.ItemId, code.Code);
                    }
                }
            }

            existingCodes.Add((0, code.ItemId, code.Code));
            await session.SaveAsync(code, false, PayrollConstants.CollectionName);
            codes++;
        }

        if (dropLegacyTables)
        {
            await DropLegacyIndexTablesAsync(schemaBuilder);
        }

        await DeleteLegacyDocumentsAsync(schemaBuilder, documentTable, handled);

        return new MoveResult(schedules, codes, left, renamed);
    }

    // A code exactly as Pay Setup seeded it, never changed since.
    private static bool IsUntouchedSeed(PayCode code)
        => code.ModifiedUtc is null
            && PayCodeSeeder.Defaults.Any(seed => string.Equals(seed.Code, code.Code, StringComparison.Ordinal)
                && string.Equals(seed.Name, code.Name, StringComparison.Ordinal)
                && string.Equals(seed.Kind, code.Kind, StringComparison.Ordinal)
                && seed.Multiplier == code.Multiplier
                && code.Class == (seed.Kind == PayCodeKinds.Unpaid ? PayCodeClass.Memo : PayCodeClass.Earning));

    // The code with the suffix, numbered when that is taken too.
    private static string FreeCode(string code, List<(long DocumentId, string ItemId, string Code)> existing)
    {
        for (var number = 1; ; number++)
        {
            var candidate = code + RenamedSuffix + (number == 1 ? string.Empty : number.ToString(CultureInfo.InvariantCulture));

            if (!existing.Any(row => string.Equals(row.Code, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }
    }

    // A document that isn't JSON, or not the shape expected, is one the move can't read.
    private static T TryRead<T>(Func<T> read)
        where T : class
    {
        try
        {
            return read();
        }
        catch (JsonException)
        {
            return null;
        }
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
            schemaBuilder.TablePrefix + schemaBuilder.TableNameConvention.GetIndexTable(typeof(PayrollPayCodeIndex), PayrollConstants.CollectionName),
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

    private static async Task<List<(long Id, string Content)>> ReadAsync(ISchemaBuilder schemaBuilder, string documentTable, string typeName)
    {
        var dialect = schemaBuilder.Dialect;
        var contents = new List<(long Id, string Content)>();

        await using var command = schemaBuilder.Connection.CreateCommand();

        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = $"SELECT {dialect.QuoteForColumnName("Id")}, {dialect.QuoteForColumnName("Content")} FROM {documentTable} WHERE {dialect.QuoteForColumnName("Type")} = @type ORDER BY {dialect.QuoteForColumnName("Id")}";
        AddParameter(command, "@type", typeName);

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            contents.Add((Convert.ToInt64(reader.GetValue(0), CultureInfo.InvariantCulture), reader.IsDBNull(1) ? string.Empty : reader.GetString(1)));
        }

        return contents;
    }

    // The tables held the old documents' index rows; the Payroll collection has its own.
    private static async Task DropLegacyIndexTablesAsync(ISchemaBuilder schemaBuilder)
    {
        await schemaBuilder.DropTableAsync("Workforce_PayPeriodCalendarIndex");
        await schemaBuilder.DropTableAsync("Workforce_PayCodeIndex");
    }

    // Only the old documents whose record is now in the Payroll collection go, one by one by their document id.
    private static async Task DeleteLegacyDocumentsAsync(ISchemaBuilder schemaBuilder, string documentTable, IEnumerable<long> documentIds)
    {
        var dialect = schemaBuilder.Dialect;

        foreach (var documentId in documentIds)
        {
            await using var command = schemaBuilder.Connection.CreateCommand();

            command.Transaction = schemaBuilder.Transaction;
            command.CommandText = $"DELETE FROM {documentTable} WHERE {dialect.QuoteForColumnName("Id")} = @id AND {dialect.QuoteForColumnName("Type")} IN (@schedule, @code)";
            AddParameter(command, "@id", documentId);
            AddParameter(command, "@schedule", LegacyPayPeriodTypeName);
            AddParameter(command, "@code", LegacyPayCodeTypeName);

            await command.ExecuteNonQueryAsync();
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();

        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
