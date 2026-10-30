using CrestApps.Core.AI.Services;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.AI.Migrations;

/// <summary>
/// Adds the default speaking styles and guardrail topics as System Prompt templates the admin can edit or delete.
/// </summary>
internal sealed class AISystemPromptTemplateDefaultsMigrations : DataMigration
{
    private readonly IAISystemPromptTemplateSeeder _seeder;

    /// <summary>
    /// Initializes a new instance of the <see cref="AISystemPromptTemplateDefaultsMigrations"/> class.
    /// </summary>
    /// <param name="seeder">The seeder of the default System Prompt templates.</param>
    public AISystemPromptTemplateDefaultsMigrations(IAISystemPromptTemplateSeeder seeder)
    {
        _seeder = seeder;
    }

    /// <summary>
    /// Adds each default template no saved template has the name of yet.
    /// </summary>
    public async Task<int> CreateAsync()
    {
        // Seeded only here, so a default the admin deleted stays deleted.
        await _seeder.SeedAsync();

        return 1;
    }
}
