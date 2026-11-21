using System.Data.Common;
using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Staff.Core.Handlers;
using CrestApps.OrchardCore.Staff.Core.Indexes;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.AspNetCore.DataProtection;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Workforce.Migrations;

/// <summary>
/// Moves Workforce workers into the Staff collection as staff members with the same ids, so every stored worker id
/// (punches, timesheets, shifts, time off, visits, territories, skills) still points at the same person. Workforce's
/// own values go to <see cref="WorkforceStaffSettings"/>, pay rates to <see cref="StaffPayRates"/> and the kiosk PIN to
/// <see cref="StaffBadge"/>. The old index tables stay, empty; the old documents are deleted once copied. A feed secret
/// stored in plain text gets its hash and protected copy as the worker moves: the portal's own backfill may have run
/// before the move, on the start that turned Staff on, and found no worker to upgrade.
/// </summary>
internal static class WorkforceWorkersMove
{
    public const string LegacyTypeName = "CrestApps.OrchardCore.Workforce.Models.WorkerProfile, CrestApps.OrchardCore.Workforce.Abstractions";

    public static async Task<IReadOnlyList<StaffMember>> MoveAsync(ISchemaBuilder schemaBuilder, IStore store, ISession session, IDataProtectionProvider dataProtectionProvider = null)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(session);

        var dialect = schemaBuilder.Dialect;
        var documentTable = dialect.QuoteForTableName(
            schemaBuilder.TablePrefix + schemaBuilder.TableNameConvention.GetDocumentTable(WorkforceConstants.CollectionName),
            store.Configuration.Schema);
        var idColumn = dialect.QuoteForColumnName("Id");
        var typeColumn = dialect.QuoteForColumnName("Type");
        var contentColumn = dialect.QuoteForColumnName("Content");
        var contents = new List<string>();

        await using (var command = schemaBuilder.Connection.CreateCommand())
        {
            command.Transaction = schemaBuilder.Transaction;
            command.CommandText = $"SELECT {contentColumn} FROM {documentTable} WHERE {typeColumn} = @type ORDER BY {idColumn}";
            AddParameter(command, "@type", LegacyTypeName);

            await using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                contents.Add(reader.GetString(0));
            }
        }

        var existingIds = (await session.QueryIndex<StaffMemberIndex>(collection: StaffConstants.CollectionName).ListAsync())
            .Select(index => index.ItemId)
            .ToHashSet(StringComparer.Ordinal);
        var moved = new List<StaffMember>();

        foreach (var content in contents)
        {
            if (Convert(content, store.Configuration.ContentSerializer) is not { } staff || string.IsNullOrEmpty(staff.ItemId) || !existingIds.Add(staff.ItemId))
            {
                continue;
            }

            if (dataProtectionProvider is not null)
            {
                WorkerFeedTokenBackfill.Upgrade(staff, dataProtectionProvider);
            }

            await session.SaveAsync(staff, false, StaffConstants.CollectionName);
            moved.Add(staff);
        }

        await DeleteLegacyDocumentsAsync(schemaBuilder, documentTable, typeColumn);

        return moved;
    }

    /// <summary>
    /// Turns a stored worker document into a staff member.
    /// </summary>
    /// <param name="content">The worker's JSON.</param>
    /// <param name="serializer">The store's content serializer, which wrote the JSON.</param>
    /// <returns>The staff member, or <see langword="null"/> when the JSON can't be read.</returns>
    internal static StaffMember Convert(string content, IContentSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);

        if (JsonNode.Parse(content) is not JsonObject worker)
        {
            return null;
        }

        // The manager was a worker id; workers keep their ids as staff members.
        if (Find(worker, "ManagerWorkerId") is { } manager)
        {
            worker[Key(worker, "ManagerWorkerId").Replace("WorkerId", "Id", StringComparison.OrdinalIgnoreCase)] = manager.DeepClone();
        }

        if (Find(worker, "Properties") is JsonObject properties)
        {
            Rename(properties, "WorkerPayRates", nameof(StaffPayRates));
            Rename(properties, "WorkerTimeClockSettings", nameof(StaffBadge));
        }

        if (serializer.Deserialize(worker.ToJsonString(), typeof(StaffMember)) is not StaffMember staff)
        {
            return null;
        }

        // The scheduling values sat on the worker itself; the same names read them into Workforce's settings.
        var settings = serializer.Deserialize(worker.ToJsonString(), typeof(WorkforceStaffSettings)) as WorkforceStaffSettings ?? new WorkforceStaffSettings();

        settings.Availability ??= [];
        staff.Put(settings);

        StaffRecords.Tidy(staff);
        staff.Put(StaffSavedState.From(staff));

        return staff;
    }

    private static void Rename(JsonObject properties, string from, string to)
    {
        if (Find(properties, from) is { } value && Find(properties, to) is null)
        {
            properties[to] = value.DeepClone();
            properties.Remove(Key(properties, from));
        }
    }

    private static string Key(JsonObject json, string name)
        => json.Select(pair => pair.Key).FirstOrDefault(key => string.Equals(key, name, StringComparison.OrdinalIgnoreCase)) ?? name;

    private static JsonNode Find(JsonObject json, string name)
        => json[Key(json, name)];

    // Every old document has been saved again in the Staff collection with the same id, so only the old copies go.
    private static async Task DeleteLegacyDocumentsAsync(ISchemaBuilder schemaBuilder, string documentTable, string typeColumn)
    {
        await using var command = schemaBuilder.Connection.CreateCommand();

        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = $"DELETE FROM {documentTable} WHERE {typeColumn} = @type";
        AddParameter(command, "@type", LegacyTypeName);

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
