using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Saves again, a page per run, every version indexed before the version index held its publisher and notes, so the
/// versions list shows them without loading versions. The content of a version does not change. Until a version is
/// reached, the list reads it itself. <see cref="SurveysMigrations"/> asks for it when it adds the columns.
/// </summary>
internal sealed class SurveyVersionsReindexStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "Surveys.SurveyVersionsReindex";

    private readonly ISession _session;
    private readonly SurveyVersionStore _versionStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyVersionsReindexStep"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="versionStore">The version store.</param>
    public SurveyVersionsReindexStep(ISession session, SurveyVersionStore versionStore)
    {
        _session = session;
        _versionStore = versionStore;
    }

    /// <inheritdoc />
    public string Name => StepName;

    /// <inheritdoc />
    public Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
        => SurveyUpgradePages.RunAsync(
            context,
            cursor => SurveyUpgradePages.VisitPageAsync(_session, _versionStore, SurveysConstants.CollectionName, cursor, _ => true, index => index.PublishedBy == null, cancellationToken: cancellationToken));
}
