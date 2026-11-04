using System.Data.Common;
using CrestApps.Core;
using CrestApps.OrchardCore.Teams;
using CrestApps.OrchardCore.Teams.Core.Indexes;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Workforce.Models;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Workforce.Migrations;

/// <summary>
/// Moves the teams Workforce kept in its own collection, before teams had their own module, to the Teams collection.
/// </summary>
/// <remarks>
/// Each team is read from its old document as JSON, because its old type no longer exists, and saved as a
/// <see cref="Team"/> with the same id, so the memberships, schedules and settings that name it still find it. What only
/// Workforce uses (the site, the calendar, the crew lead and the scheduling and time clock policies) goes into the
/// team's <see cref="WorkforceTeamSettings"/>. A team already in the Teams collection is left as it is. The old
/// documents and the old index table are then removed. Everything runs on the migration's own connection and
/// transaction: a second connection opened inside a migration step waits on the lock the step already holds, which on
/// SQLite never resolves.
/// </remarks>
internal static class WorkforceTeamsMove
{
    /// <summary>
    /// The type name YesSql stored on each old team document.
    /// </summary>
    public const string LegacyTypeName = "CrestApps.OrchardCore.Workforce.Models.WorkforceTeam, CrestApps.OrchardCore.Workforce.Abstractions";

    /// <summary>
    /// Moves the teams.
    /// </summary>
    /// <param name="schemaBuilder">The migration's schema builder, whose connection and transaction the move uses.</param>
    /// <param name="store">The store, for its table naming and content serializer.</param>
    /// <param name="session">The migration's session, which saves the moved teams.</param>
    /// <returns>The number of teams moved.</returns>
    public static async Task<int> MoveAsync(ISchemaBuilder schemaBuilder, IStore store, ISession session)
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

        var serializer = store.Configuration.ContentSerializer;
        var existingIds = (await session.QueryIndex<TeamIndex>(collection: TeamsConstants.CollectionName).ListAsync())
            .Select(index => index.ItemId)
            .ToHashSet(StringComparer.Ordinal);
        var moved = 0;

        foreach (var content in contents)
        {
            if (serializer.Deserialize(content, typeof(Team)) is not Team team ||
                string.IsNullOrEmpty(team.ItemId) ||
                !existingIds.Add(team.ItemId))
            {
                continue;
            }

            if (serializer.Deserialize(content, typeof(WorkforceTeamSettings)) is WorkforceTeamSettings settings)
            {
                team.Put(settings);
            }

            await session.SaveAsync(team, false, TeamsConstants.CollectionName);
            moved++;
        }

        await DropLegacyIndexTableAsync(schemaBuilder);
        await DeleteLegacyDocumentsAsync(schemaBuilder, documentTable, typeColumn);

        return moved;
    }

    // The table held the old documents' index rows; the Teams collection has its own.
    private static Task DropLegacyIndexTableAsync(ISchemaBuilder schemaBuilder)
    {
        return schemaBuilder.DropTableAsync("Workforce_TeamIndex");
    }

    // Every old document has been saved again in the Teams collection with the same id, so only the old copies go.
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
