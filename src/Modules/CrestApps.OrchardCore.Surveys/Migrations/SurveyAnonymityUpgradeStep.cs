using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Upgrades the anonymity data of sites made before it was hardened, a page per run: protects every anonymity secret
/// still stored in plain text with ASP.NET Data Protection (keeping its value, so limits and invitation links keep
/// working), gives a secret to any survey without one, and clears the exact times kept on the participation records of
/// anonymous surveys, which keep the day only. <see cref="SurveyAnonymityMigrations"/> asks for it. Secrets not reached
/// yet are still read. The survey and participation stores make the same change on every save, and both walks are
/// idempotent, so they have no upper bound: they also reach the records saved while they run, such as the surveys the
/// sanitizing step saves in the same pass.
/// </summary>
internal sealed class SurveyAnonymityUpgradeStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "Surveys.AnonymityUpgrade";

    private const string AnonymousKeyPrefix = SurveyRespondentKeys.AnonymousPrefix;

    private readonly ISession _session;
    private readonly SurveyStore _surveyStore;
    private readonly SurveyParticipationStore _participationStore;
    private readonly SurveyAnonymitySecrets _secrets;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyAnonymityUpgradeStep"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="surveyStore">The survey store.</param>
    /// <param name="participationStore">The participation store.</param>
    /// <param name="secrets">The anonymity secrets.</param>
    public SurveyAnonymityUpgradeStep(
        ISession session,
        SurveyStore surveyStore,
        SurveyParticipationStore participationStore,
        SurveyAnonymitySecrets secrets)
    {
        _session = session;
        _surveyStore = surveyStore;
        _participationStore = participationStore;
        _secrets = secrets;
    }

    /// <inheritdoc />
    public string Name => StepName;

    /// <inheritdoc />
    public Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
        => SurveyUpgradePages.RunAsync(
            context,
            cursor => SurveyUpgradePages.VisitPageAsync(_session, _surveyStore, SurveysConstants.CollectionName, cursor, _secrets.EnsureProtected, idempotent: true, cancellationToken: cancellationToken),
            cursor => SurveyUpgradePages.VisitPageAsync(
                _session,
                _participationStore,
                SurveysConstants.CollectionName,
                cursor,
                row =>
                {
                    if (row.LastCompletedUtc is null)
                    {
                        return false;
                    }

                    row.LastCompletedUtc = null;

                    return true;
                },
                index => index.RespondentKey.StartsWith(AnonymousKeyPrefix),
                idempotent: true,
                cancellationToken: cancellationToken));
}
