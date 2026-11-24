using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Fills the reminder count of the invitation index for the invitations reminded before the column existed: walks only
/// the open invitations (sent, opened or started) that had a reminder, a large page per run, and saves those, so their
/// index row gets <see cref="SurveyInvitationIndex.RemindersSent"/> and the reminder task's due query stops reading the
/// ones that used up their reminders. Until a row is reached, the reminder task reads it, sees in the invitation that
/// no reminder is due and skips it, as before. <see cref="SurveysMigrations"/> asks for it when it adds the columns.
/// </summary>
/// <remarks>
/// An invitation is saved only when nobody saved it after the run read its index row (<see cref="SurveyUpgradePages.SaveUnchangedAsync"/>):
/// a reminder, a completion or an opt-out saved while the run holds the page is never overwritten, and that save already
/// filled the column.
/// </remarks>
internal sealed class SurveyInvitationRemindersStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "Surveys.InvitationReminders";

    /// <summary>
    /// The number of invitations read in one run.
    /// </summary>
    public const int PageSize = 1000;

    private static readonly string[] _open =
    [
        nameof(SurveyInvitationStatus.Sent),
        nameof(SurveyInvitationStatus.Opened),
        nameof(SurveyInvitationStatus.Started),
    ];

    private readonly ISession _session;
    private readonly SurveyInvitationStore _invitationStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationRemindersStep"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="invitationStore">The invitation store.</param>
    public SurveyInvitationRemindersStep(ISession session, SurveyInvitationStore invitationStore)
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
            cursor => SurveyUpgradePages.VisitPageAsync(
                _session,
                _invitationStore,
                SurveysConstants.CollectionName,
                cursor,
                invitation => invitation.RemindersSent > 0,
                index => index.Status.IsIn(_open) && index.LastReminderUtc != null && index.RemindersSent == 0,
                PageSize,
                cancellationToken: cancellationToken));
}
