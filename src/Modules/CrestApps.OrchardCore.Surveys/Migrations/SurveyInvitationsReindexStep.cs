using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Saves every invitation again, a page per run, so the index columns added after invitations were indexed (the seal
/// lookup, the e-mail and the last reminder) are filled from each invitation. <see cref="SurveysMigrations"/> asks for
/// it when it adds the columns.
/// </summary>
internal sealed class SurveyInvitationsReindexStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "Surveys.InvitationsReindex";

    private readonly ISession _session;
    private readonly SurveyInvitationStore _invitationStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationsReindexStep"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="invitationStore">The invitation store.</param>
    public SurveyInvitationsReindexStep(ISession session, SurveyInvitationStore invitationStore)
    {
        _session = session;
        _invitationStore = invitationStore;
    }

    /// <inheritdoc />
    public string Name => StepName;

    /// <inheritdoc />
    public Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
        => SurveyUpgradePages.RunAsync(
            context,
            cursor => SurveyUpgradePages.VisitPageAsync(_session, _invitationStore, SurveysConstants.CollectionName, cursor, _ => true, cancellationToken: cancellationToken));
}
