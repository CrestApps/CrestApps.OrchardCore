using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using OrchardCore.Data.Migration;
using OrchardCore.Recipes;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Creates the index tables of Surveys and seeds the default survey templates.
/// </summary>
internal sealed class SurveysMigrations : DataMigration
{
    private readonly IRecipeMigrator _recipeMigrator;
    private readonly SurveyInvitationStore _invitationStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysMigrations"/> class.
    /// </summary>
    /// <param name="recipeMigrator">The recipe migrator.</param>
    /// <param name="invitationStore">The invitation store, to index existing invitations again.</param>
    public SurveysMigrations(IRecipeMigrator recipeMigrator, SurveyInvitationStore invitationStore)
    {
        _recipeMigrator = recipeMigrator;
        _invitationStore = invitationStore;
    }

    /// <summary>
    /// Creates the index tables and runs the recipe of the default templates.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await SurveysIndexSchema.CreateAsync(SchemaBuilder);
        await _recipeMigrator.ExecuteAsync($"survey-templates{RecipesConstants.RecipeExtension}", this);

        return 2;
    }

    /// <summary>
    /// Adds the seal lookup and e-mail columns of the invitation index (one indexed query finds the invitation of an
    /// anonymous response, and personal data export and erasure find guest invitations by e-mail), then indexes the
    /// existing invitations again so their e-mail column is filled.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SurveysIndexSchema.AddInvitationLookupColumnsAsync(SchemaBuilder);

        foreach (var invitation in await _invitationStore.GetAllAsync())
        {
            await _invitationStore.UpdateAsync(invitation);
        }

        return 2;
    }
}
