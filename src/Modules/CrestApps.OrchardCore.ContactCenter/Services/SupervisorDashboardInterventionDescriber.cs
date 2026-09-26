using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Describes, for one supervisor's live dashboard, what they can do about each agent's call: whether they are listening
/// to it and how, which interventions its provider and their permissions allow, and -- for a call they cannot monitor
/// at all -- why not.
/// </summary>
internal sealed class SupervisorDashboardInterventionDescriber
{
    /// <summary>The supervisor can take the call over.</summary>
    public const string TakeOver = "TakeOver";

    /// <summary>The supervisor can end the call.</summary>
    public const string EndCall = "EndCall";

    /// <summary>The supervisor can transfer the call.</summary>
    public const string Transfer = "Transfer";

    /// <summary>The supervisor can turn the call's recording on or off.</summary>
    public const string Record = "Record";

    // How many active phone calls are read to find agents on a call that is not a Contact Center interaction.
    private const int ActiveCallScanLimit = 500;

    private readonly ICallSessionManager _callSessionManager;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly bool _canRecord;
    private readonly ITelephonyInteractionStore _telephonyInteractions;

    internal readonly IStringLocalizer S;

    public SupervisorDashboardInterventionDescriber(
        ICallSessionManager callSessionManager,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IEnumerable<IContactCenterRecordingService> recordingServices,
        IEnumerable<ITelephonyInteractionStore> telephonyInteractions,
        IStringLocalizer<SupervisorDashboardInterventionDescriber> stringLocalizer)
    {
        _callSessionManager = callSessionManager;
        _voiceProviderResolver = voiceProviderResolver;
        _canRecord = recordingServices.Any();
        _telephonyInteractions = telephonyInteractions.FirstOrDefault();
        S = stringLocalizer;
    }

    /// <summary>
    /// Finds which of the given users are on a phone call right now -- one placed from the keypad, an extension call --
    /// that the platform did not route as a Contact Center interaction.
    /// </summary>
    /// <param name="userIds">The users with no live Contact Center interaction.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The users on such a call.</returns>
    public async Task<ISet<string>> FindUsersOnOtherCallsAsync(IEnumerable<string> userIds, CancellationToken cancellationToken)
    {
        var wanted = new HashSet<string>(userIds.Where(userId => !string.IsNullOrEmpty(userId)), StringComparer.Ordinal);

        if (wanted.Count == 0 || _telephonyInteractions is null)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var active = await _telephonyInteractions.GetActiveAsync(ActiveCallScanLimit, cancellationToken);

        return active
            .Where(call => call is not null && wanted.Contains(call.UserId))
            .Select(call => call.UserId)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Fills in what the supervisor can do about an agent's call.
    /// </summary>
    /// <param name="row">The agent's row.</param>
    /// <param name="interaction">The agent's live interaction the supervisor may see, or <see langword="null"/>.</param>
    /// <param name="supervisorUserId">The supervisor.</param>
    /// <param name="canIntervene">Whether the supervisor holds the intervention permission.</param>
    /// <param name="onOtherCall">Whether the agent is on a call that is not a Contact Center interaction.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task DescribeAsync(
        SupervisorAgentViewModel row,
        Interaction interaction,
        string supervisorUserId,
        bool canIntervene,
        bool onOtherCall,
        CancellationToken cancellationToken)
    {
        if (interaction is null)
        {
            if (onOtherCall)
            {
                row.MonitoringUnavailableReason = S["On a call that is not a Contact Center interaction (a number dialed from the soft phone or an extension call), so it cannot be monitored."];
            }

            return;
        }

        row.RecordingState = interaction.RecordingState.ToString();

        var session = await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken);
        var engagement = session?.ActiveMonitorSessions.FirstOrDefault(monitorSession =>
            string.Equals(monitorSession.SupervisorUserId, supervisorUserId, StringComparison.Ordinal));

        if (engagement is not null)
        {
            row.MonitorMode = engagement.Mode.ToString();
            row.MonitorConnected = engagement.ConnectedUtc.HasValue;
        }

        if (interaction.RecordingState == RecordingState.Paused)
        {
            row.MonitoringUnavailableReason = S["A sensitive-data capture is in progress; monitoring is unavailable until it completes."];
        }
        else if (row.AvailableMonitoringModes.Count == 0)
        {
            row.MonitoringUnavailableReason = S["This call's voice provider does not support monitoring."];
        }

        var provider = _voiceProviderResolver.Get(interaction.ProviderName);

        if (!canIntervene || provider is null || string.IsNullOrEmpty(interaction.ProviderInteractionId))
        {
            return;
        }

        if (provider is IContactCenterVoiceSupervisorInterventionProvider)
        {
            row.AvailableInterventions.Add(TakeOver);
        }

        row.AvailableInterventions.Add(EndCall);

        if (provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.CallTransfer))
        {
            row.AvailableInterventions.Add(Transfer);
        }

        if (_canRecord && provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.Recording))
        {
            row.AvailableInterventions.Add(Record);
        }
    }
}
