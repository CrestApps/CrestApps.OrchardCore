using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using OrchardCore.Recipes;
using OrchardCore.Recipes.Services;
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
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AgentStateReasonCodeIndex>(table => table
            .CreateIndex("IDX_AgentStateReasonCodeIndex_DocumentId", "DocumentId", "ItemId", "Enabled"),
            collection: ContactCenterConstants.CollectionName
        );

        await _recipeMigrator.ExecuteAsync($"agent-state-reason-codes{RecipesConstants.RecipeExtension}", this);

        return 1;
    }
}
