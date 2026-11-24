using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Runtime;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using OrchardCore.Data.Migration;
using OrchardCore.Modules;
using OrchardCore.Recipes;
using OrchardCore.Recipes.Services;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Creates the index tables of Surveys and seeds the default survey templates. A new site gets the final tables at once
/// and runs no update step; the update steps only change the schema of a site made with an earlier version of the
/// tables, and ask for a resumable step for any data that has to follow, so the tenant's activation never waits on it.
/// </summary>
internal sealed class SurveysMigrations : DataMigration
{
    private readonly IRecipeMigrator _recipeMigrator;
    private readonly ResumableSteps _steps;
    private readonly IStore _store;
    private readonly ISession _session;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysMigrations"/> class.
    /// </summary>
    /// <param name="recipeMigrator">The recipe migrator.</param>
    /// <param name="steps">The resumable upgrade steps.</param>
    /// <param name="store">The YesSql store, for the database schema of the tenant's tables.</param>
    /// <param name="session">The YesSql session.</param>
    /// <param name="clock">The clock.</param>
    public SurveysMigrations(IRecipeMigrator recipeMigrator, ResumableSteps steps, IStore store, ISession session, IClock clock)
    {
        _recipeMigrator = recipeMigrator;
        _steps = steps;
        _store = store;
        _session = session;
        _clock = clock;
    }

    /// <summary>
    /// Creates the final index tables and runs the recipe of the default templates.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> CreateAsync()
    {
        await SurveysIndexSchema.CreateAsync(SchemaBuilder);
        await _recipeMigrator.ExecuteAsync($"survey-templates{RecipesConstants.RecipeExtension}", this);

        return 13;
    }

    /// <summary>
    /// Adds the seal lookup and e-mail columns of the invitation index (one indexed query finds the invitation of an
    /// anonymous response, and personal data export and erasure find guest invitations by e-mail), and asks for the
    /// step that indexes the existing invitations again so their columns are filled.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SurveysIndexSchema.AddInvitationLookupColumnsAsync(SchemaBuilder);
        await _steps.RequestAsync(SurveyInvitationsReindexStep.StepName);

        return 2;
    }

    /// <summary>
    /// Adds the upkeep columns of the response index (anonymized by retention, and failed upkeep runs). They are not
    /// nullable, so existing rows start at not anonymized and no failures on every database; retention marks a response
    /// it anonymized before on its next visit.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await SurveysIndexSchema.AddResponseUpkeepColumnsAsync(SchemaBuilder);

        return 3;
    }

    /// <summary>
    /// Sets the upkeep columns to their defaults where they are empty: an earlier version of <see cref="UpdateFrom2Async"/>
    /// added them as nullable, which left the rows already there empty on SQL Server, so upkeep skipped them.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom3Async()
    {
        await SurveysIndexSchema.FillResponseUpkeepColumnsAsync(SchemaBuilder, _store.Configuration.Schema);

        return 4;
    }

    /// <summary>
    /// Adds the hold column of the invitation index (schema only), so the reminder task reads held first sends directly,
    /// and asks for the step that marks the invitations held before the column existed. That step reads only pending
    /// invitations and saves only the held ones, so they are marked within its first runs.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom4Async()
    {
        await SurveysIndexSchema.AddInvitationHoldColumnAsync(SchemaBuilder);
        await _steps.RequestAsync(SurveyInvitationHoldsStep.StepName);

        return 5;
    }

    /// <summary>
    /// Adds the user owner and anonymity columns of the survey index and the survey tag index (schema only), so the
    /// survey list, the reports and the data sets read surveys from the index instead of loading every survey, and asks
    /// for the step that indexes the existing surveys again so the columns are filled.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom5Async()
    {
        await SurveysIndexSchema.AddSurveyListColumnsAsync(SchemaBuilder);
        await _steps.RequestAsync(SurveysReindexStep.StepName);

        return 6;
    }

    /// <summary>
    /// Adds the reminder columns of the invitation index (schema only), so the reminder task reads only the invitations
    /// due a reminder, and asks for the step that fills the reminder count of the invitations reminded before.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom6Async()
    {
        await SurveysIndexSchema.AddInvitationReminderColumnsAsync(SchemaBuilder);
        await _steps.RequestAsync(SurveyInvitationRemindersStep.StepName);

        return 7;
    }

    /// <summary>
    /// Records when the upkeep's close step started on this site (<see cref="SurveyCloseStepStart"/>), so it leaves alone
    /// the responses of surveys that were already closed before the upgrade. One document, no data walk.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom7Async()
    {
        // A migration step runs once per site, so the document is written once.
        await _session.SaveAsync(new SurveyCloseStepStart { StartedUtc = _clock.UtcNow }, false, SurveysConstants.CollectionName);

        return 8;
    }

    /// <summary>
    /// Adds the upkeep columns of the survey index (retention days, closing date, total limit), so upkeep reads only the
    /// surveys each of its steps needs, and asks for the step that fills them from each survey.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom8Async()
    {
        await SurveysIndexSchema.AddSurveyUpkeepColumnsAsync(SchemaBuilder);
        await _steps.RequestAsync(SurveyUpkeepReindexStep.StepName);

        return 9;
    }

    /// <summary>
    /// Adds the index of the invitation opt-out records and asks for the step that moves the opt-outs of the old single
    /// list into them.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom9Async()
    {
        await SurveysIndexSchema.CreateInvitationOptOutTableAsync(SchemaBuilder);
        await _steps.RequestAsync(SurveyInvitationOptOutMoveStep.StepName);

        return 10;
    }

    /// <summary>
    /// Adds the publisher and notes columns of the version index, so the versions list loads no version, and asks for
    /// the step that fills them.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom10Async()
    {
        await SurveysIndexSchema.AddVersionListColumnsAsync(SchemaBuilder);
        await _steps.RequestAsync(SurveyVersionsReindexStep.StepName);

        return 11;
    }

    /// <summary>
    /// Adds the database indexes of the invitation index that find a list's invitation of a user or a recipient, and
    /// that count the day's invitation e-mails (schema only: the columns are already filled).
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom11Async()
    {
        await SurveysIndexSchema.AddInvitationRecipientIndexesAsync(SchemaBuilder);

        return 12;
    }

    /// <summary>
    /// Adds the delete mark column of the survey index, so a start reads the mark by the index instead of loading the
    /// survey (schema only: surveys are marked only for the few minutes a delete runs, and every save writes the column).
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom12Async()
    {
        await SurveysIndexSchema.AddSurveyDeletingColumnAsync(SchemaBuilder);

        return 13;
    }
}