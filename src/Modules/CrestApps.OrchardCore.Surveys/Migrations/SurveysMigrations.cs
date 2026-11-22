using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using OrchardCore.Data.Migration;
using OrchardCore.Recipes;
using OrchardCore.Recipes.Services;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Creates the index tables of Surveys and seeds the default survey templates.
/// </summary>
internal sealed class SurveysMigrations : DataMigration
{
    private readonly IRecipeMigrator _recipeMigrator;
    private readonly SurveyInvitationStore _invitationStore;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysMigrations"/> class.
    /// </summary>
    /// <param name="recipeMigrator">The recipe migrator.</param>
    /// <param name="invitationStore">The invitation store, to index existing invitations again.</param>
    /// <param name="session">The YesSql session.</param>
    public SurveysMigrations(IRecipeMigrator recipeMigrator, SurveyInvitationStore invitationStore, ISession session)
    {
        _recipeMigrator = recipeMigrator;
        _invitationStore = invitationStore;
        _session = session;
    }

    /// <summary>
    /// Creates the index tables and runs the recipe of the default templates.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await SurveysIndexSchema.CreateAsync(SchemaBuilder);
        await _recipeMigrator.ExecuteAsync($"survey-templates{RecipesConstants.RecipeExtension}", this);

        return 3;
    }

    /// <summary>
    /// Adds the seal lookup and e-mail columns of the invitation index (one indexed query finds the invitation of an
    /// anonymous response, and personal data export and erasure find guest invitations by e-mail), then indexes the
    /// existing invitations again, a page at a time, so their e-mail column is filled.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SurveysIndexSchema.AddInvitationLookupColumnsAsync(SchemaBuilder);
        await SurveyMigrationPages.ForEachAsync(_session, _invitationStore, SurveysConstants.CollectionName, _ => true);

        return 2;
    }

    /// <summary>
    /// Adds the upkeep columns of the response index (anonymized by retention, and failed upkeep runs). Existing rows
    /// start at not anonymized and no failures; retention marks a response it anonymized before on its next visit.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await SurveysIndexSchema.AddResponseUpkeepColumnsAsync(SchemaBuilder);

        return 3;
    }
}
