using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Saves again, a page per run, every survey indexed before the survey index held its retention days, closing date and
/// total limit, so upkeep can find the surveys each of its steps needs by the index. Until a survey is reached, upkeep
/// reads it itself. <see cref="SurveysMigrations"/> asks for it when it adds the columns.
/// </summary>
internal sealed class SurveyUpkeepReindexStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "Surveys.SurveyUpkeepReindex";

    private readonly ISession _session;
    private readonly SurveyStore _surveyStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyUpkeepReindexStep"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="surveyStore">The survey store.</param>
    public SurveyUpkeepReindexStep(ISession session, SurveyStore surveyStore)
    {
        _session = session;
        _surveyStore = surveyStore;
    }

    /// <inheritdoc />
    public string Name => StepName;

    /// <inheritdoc />
    public Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
        => SurveyUpgradePages.RunAsync(
            context,
            cursor => SurveyUpgradePages.VisitPageAsync(_session, _surveyStore, SurveysConstants.CollectionName, cursor, _ => true, index => index.RetentionDays == null, cancellationToken: cancellationToken));
}
