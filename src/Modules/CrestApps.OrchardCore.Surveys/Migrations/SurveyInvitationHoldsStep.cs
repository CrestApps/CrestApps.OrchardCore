using CrestApps.Core;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Invitations;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Migrations;

/// <summary>
/// Marks the invitations held back before the invitation index had its hold column: walks only the
/// <see cref="SurveyInvitationStatus.Pending"/> invitations (the only ones a hold applies to), a large page per run, and
/// saves only those that carry a <see cref="SurveyInvitationHold"/>, so their index row gets
/// <see cref="SurveyInvitationIndex.Held"/> and the reminder task finds them within the first runs.
/// <see cref="SurveysMigrations"/> asks for it when it adds the column.
/// </summary>
internal sealed class SurveyInvitationHoldsStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "Surveys.InvitationHolds";

    /// <summary>
    /// The number of invitations read in one run; few of them are saved.
    /// </summary>
    public const int PageSize = 1000;

    private readonly ISession _session;
    private readonly SurveyInvitationStore _invitationStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationHoldsStep"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="invitationStore">The invitation store.</param>
    public SurveyInvitationHoldsStep(ISession session, SurveyInvitationStore invitationStore)
    {
        _session = session;
        _invitationStore = invitationStore;
    }

    /// <inheritdoc />
    public string Name => StepName;

    /// <inheritdoc />
    public Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
    {
        var pending = nameof(SurveyInvitationStatus.Pending);

        return SurveyUpgradePages.RunAsync(
            context,
            cursor => SurveyUpgradePages.VisitPageAsync(
                _session,
                _invitationStore,
                SurveysConstants.CollectionName,
                cursor,
                IsHeld,
                index => index.Status == pending && index.Held == false,
                PageSize,
                save: _invitationStore.SaveUnchangedAsync,
                cancellationToken: cancellationToken));
    }

    // Saving the invitation writes its index row again, which sets the hold column from the hold mark.
    private static bool IsHeld(SurveyInvitation invitation)
        => invitation.Status == SurveyInvitationStatus.Pending && invitation.TryGet<SurveyInvitationHold>(out var hold) && hold is not null;
}
