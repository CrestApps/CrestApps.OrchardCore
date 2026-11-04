using CrestApps.OrchardCore.Teams.Models;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Teams.Core.Indexes;

/// <summary>
/// Creates the Teams index table, for the module's migration and the tests.
/// </summary>
public static class TeamsIndexSchema
{
    /// <summary>
    /// Creates the index table in the <see cref="TeamsConstants.CollectionName"/> collection.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = TeamsConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<TeamIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<TeamKind>("Kind")
            .Column<string>("ParentId", column => column.Nullable().WithLength(26))
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<TeamIndex>(table =>
        {
            table.CreateIndex("IDX_Teams_TeamIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_Teams_TeamIndex_Parent", "ParentId");
        }, collection: collection);
    }
}
