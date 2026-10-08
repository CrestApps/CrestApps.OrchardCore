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
/// <remarks>
/// An agent's own phone call -- a number they dialed from the keypad, or an extension call at either end -- is described
/// too. It has no interaction, so the row names it with a <see cref="PhoneCallKey"/> instead, and says what the call is.
/// </remarks>
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

    private readonly ICallSessionManager _callSessionManager;
    private readonly IContactCenterVoiceProviderResolver _voiceProviderResolver;
    private readonly bool _canRecord;
    private readonly IContactCenterPhoneCallSupervisionService _phoneCalls;

    internal readonly IStringLocalizer S;

    public SupervisorDashboardInterventionDescriber(
        ICallSessionManager callSessionManager,
        IContactCenterVoiceProviderResolver voiceProviderResolver,
        IEnumerable<IContactCenterRecordingService> recordingServices,
        IContactCenterPhoneCallSupervisionService phoneCalls,
        IStringLocalizer<SupervisorDashboardInterventionDescriber> stringLocalizer)
    {
        _callSessionManager = callSessionManager;
        _voiceProviderResolver = voiceProviderResolver;
        _canRecord = recordingServices.Any();
        _phoneCalls = phoneCalls;
        S = stringLocalizer;
    }

    /// <summary>
    /// Finds which of the given users are on a phone call right now -- one placed from the keypad, an extension call at
    /// either end -- that the platform did not route as a Contact Center interaction.
    /// </summary>
    /// <param name="userIds">The users with no live Contact Center interaction.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>Each such user's call, by user.</returns>
    public Task<IReadOnlyDictionary<string, AgentPhoneCall>> FindPhoneCallsAsync(IEnumerable<string> userIds, CancellationToken cancellationToken)
        => _phoneCalls.FindCallsAsync(userIds, cancellationToken);

    /// <summary>
    /// Fills in what the supervisor can do about an agent's call.
    /// </summary>
    /// <param name="row">The agent's row.</param>
    /// <param name="interaction">The agent's live interaction the supervisor may see, or <see langword="null"/>.</param>
    /// <param name="supervisorUserId">The supervisor.</param>
    /// <param name="canIntervene">Whether the supervisor holds the intervention permission.</param>
    /// <param name="phoneCall">The agent's own phone call when they are on no interaction, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async Task DescribeAsync(
        SupervisorAgentViewModel row,
        Interaction interaction,
        string supervisorUserId,
        bool canIntervene,
        AgentPhoneCall phoneCall,
        CancellationToken cancellationToken)
    {
        if (interaction is null)
        {
            if (phoneCall is not null)
            {
                await DescribePhoneCallAsync(row, phoneCall, supervisorUserId, canIntervene, cancellationToken);
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

    // An agent's own phone call: what it is, and -- unless it is the supervisor's own, or its provider cannot join it --
    // the modes and interventions a Contact Center call offers, less the transfer and the recording, which are the
    // interaction's.
    private async Task DescribePhoneCallAsync(
        SupervisorAgentViewModel row,
        AgentPhoneCall phoneCall,
        string supervisorUserId,
        bool canIntervene,
        CancellationToken cancellationToken)
    {
        row.PhoneCall = new SupervisorPhoneCallViewModel
        {
            Direction = phoneCall.Direction.ToString(),
            Party = phoneCall.Party,
            IsExtension = phoneCall.IsExtension,
            StartedUtc = phoneCall.StartedUtc,
        };

        IReadOnlyCollection<MonitorMode> modes = string.Equals(phoneCall.UserId, supervisorUserId, StringComparison.Ordinal)
            ? []
            : _phoneCalls.GetAvailableModes(phoneCall);

        if (modes.Count == 0)
        {
            row.MonitoringUnavailableReason = S["On a phone call this voice provider cannot let a supervisor join, so it cannot be monitored."];

            return;
        }

        row.ActiveInteractionId = phoneCall.Key;
        row.AvailableMonitoringModes = modes.Select(mode => mode.ToString()).ToArray();

        var engagement = await _phoneCalls.FindEngagementAsync(phoneCall.Key, supervisorUserId, cancellationToken);

        if (engagement is not null && !engagement.TookOver)
        {
            row.MonitorMode = engagement.Mode.ToString();
            row.MonitorConnected = engagement.ConnectedUtc.HasValue;
        }

        if (!canIntervene)
        {
            return;
        }

        // Releasing either colleague ends an extension call, so only a number the agent dialed can be taken over.
        if (!phoneCall.IsExtension)
        {
            row.AvailableInterventions.Add(TakeOver);
        }

        row.AvailableInterventions.Add(EndCall);
    }
}
