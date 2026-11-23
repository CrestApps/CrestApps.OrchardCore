using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Saves again, a page per run, every survey indexed before the survey index held its user owner and anonymity, so those
/// columns and the survey tag index are filled from each survey. Until a survey is reached, readers take what they need
/// from the survey itself. <see cref="SurveysMigrations"/> asks for it when it adds the columns.
/// </summary>
internal sealed class SurveysReindexStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "Surveys.SurveysReindex";

    private readonly ISession _session;
    private readonly SurveyStore _surveyStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysReindexStep"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="surveyStore">The survey store.</param>
    public SurveysReindexStep(ISession session, SurveyStore surveyStore)
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
            cursor => SurveyUpgradePages.VisitPageAsync(_session, _surveyStore, SurveysConstants.CollectionName, cursor, _ => true, index => index.Anonymity == null, cancellationToken: cancellationToken));
}
