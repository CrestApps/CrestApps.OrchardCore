using CrestApps.Core.Data.YesSql.ContactCenter.Indexes;
using CrestApps.Core.Data.YesSql.ContactCenter.Migrations;
using OrchardCore.Data.Migration;
using OrchardCore.Recipes;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="AgentStateReasonCodeIndex"/> and seeds the standard reason codes.
/// </summary>
/// <remarks>
/// The table comes from the framework step; the seed stays here, because it is a recipe and a recipe is an
/// Orchard concept. Both still run under one migration version, so a tenant sees no difference.
/// </remarks>
internal sealed class AgentStateReasonCodeIndexMigrations : DataMigration
{
    private readonly AgentStateReasonCodeIndexMigrationsSchemaMigration _step;
    private readonly IRecipeMigrator _recipeMigrator;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentStateReasonCodeIndexMigrations"/> class.
    /// </summary>
    /// <param name="recipeMigrator">The recipe migrator used to seed the standard reason codes.</param>
    public AgentStateReasonCodeIndexMigrations(IRecipeMigrator recipeMigrator)
    {
        _step = new AgentStateReasonCodeIndexMigrationsSchemaMigration();
        _recipeMigrator = recipeMigrator;
    }

    /// <summary>
    /// Creates the reason code index table and seeds the standard reason codes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        var version = await _step.CreateAsync(SchemaBuilder);

        await _recipeMigrator.ExecuteAsync($"agent-state-reason-codes{RecipesConstants.RecipeExtension}", this);

        return version;
    }
}
