using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using OrchardCore.Recipes.Services;
using OrchardCore.Recipes;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="AgentStateReasonCodeIndex"/> and seeds the standard reason codes.
/// </summary>
internal sealed class AgentStateReasonCodeIndexMigrations : DataMigration
{
    private readonly IRecipeMigrator _recipeMigrator;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStateReasonCodeIndexMigrations"/> class.
    /// </summary>
    /// <param name="recipeMigrator">The recipe migrator used to seed the standard reason codes.</param>
    public AgentStateReasonCodeIndexMigrations(IRecipeMigrator recipeMigrator)
    {
        _recipeMigrator = recipeMigrator;
    }

    /// <summary>
    /// Creates the reason code index table and seeds the standard reason codes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AgentStateReasonCodeIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<int>("SortOrder")
            .Column<bool>("Enabled"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AgentStateReasonCodeIndex>(table => table
            .CreateIndex("IDX_AgentStateReasonCodeIndex_DocumentId", "DocumentId", "ItemId", "Enabled"),
            collection: ContactCenterStorage.CollectionName
        );

        await _recipeMigrator.ExecuteAsync($"agent-state-reason-codes{RecipesConstants.RecipeExtension}", this);

        await SchemaBuilder.AlterIndexTableAsync<AgentStateReasonCodeIndex>(table => table
            .CreateIndex(
                "IDX_AgentStateReasonCodeIndex_ItemId",
                "ItemId",
                "DocumentId"),
            collection: ContactCenterStorage.CollectionName);

        return 2;
    }

    /// <summary>
    /// Adds the index a record is found by when it is read by its own identifier. Every catalog lookup by id
    /// filters on <c>ItemId</c>, and no existing index leads with it, so each such read scanned the table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await IndexTableIndexCreation.CreateIfMissingAsync<AgentStateReasonCodeIndex>(
            SchemaBuilder,
            "IDX_AgentStateReasonCodeIndex_ItemId",
            ContactCenterStorage.CollectionName,
            "ItemId", "DocumentId");

        return 2;
    }
}
