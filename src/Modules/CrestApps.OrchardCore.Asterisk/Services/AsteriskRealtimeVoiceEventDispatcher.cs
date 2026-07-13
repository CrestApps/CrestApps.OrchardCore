using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.SignalR;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskRealtimeVoiceEventDispatcher
{
    private readonly IEnumerable<IProviderVoiceEventService> _providerVoiceEventServices;
    private readonly ITelephonyInteractionStore _telephonyInteractionStore;
    private readonly IHubContext<TelephonyHub, ITelephonyClient> _hubContext;
    private readonly IClock _clock;
    private readonly ILogger<AsteriskRealtimeVoiceEventDispatcher> _logger;
    private readonly string _tenantName;

    public AsteriskRealtimeVoiceEventDispatcher(
        IEnumerable<IProviderVoiceEventService> providerVoiceEventServices,
        ITelephonyInteractionStore telephonyInteractionStore,
        IHubContext<TelephonyHub, ITelephonyClient> hubContext,
        IClock clock,
        ILogger<AsteriskRealtimeVoiceEventDispatcher> logger,
        ShellSettings shellSettings)
    {
        _providerVoiceEventServices = providerVoiceEventServices;
        _telephonyInteractionStore = telephonyInteractionStore;
        _hubContext = hubContext;
        _clock = clock;
        _logger = logger;
        _tenantName = shellSettings.Name;
    }

    public async Task HandleAsync(AsteriskRealtimeVoiceEvent voiceEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(voiceEvent);

        if (string.IsNullOrWhiteSpace(voiceEvent.ProviderName) || string.IsNullOrWhiteSpace(voiceEvent.CallId))
        {
            return;
        }

        var providerVoiceEventService = _providerVoiceEventServices.FirstOrDefault();

        if (providerVoiceEventService is not null)
        {
            var session = await providerVoiceEventService.IngestAsync(BuildProviderVoiceEvent(voiceEvent), cancellationToken);

            if (session is not null)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Asterisk real-time event {EventType} for provider {ProviderName} call {CallId} flowed into Contact Center session {SessionId}.",
                        voiceEvent.EventType,
                        voiceEvent.ProviderName,
                        voiceEvent.CallId,
                        session.ItemId);
                }

                return;
            }
        }

        var interaction = await _telephonyInteractionStore.FindByProviderCallIdAsync(voiceEvent.ProviderName, voiceEvent.CallId, cancellationToken);

        if (interaction is null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Asterisk real-time event {EventType} for provider {ProviderName} call {CallId} did not match any telephony interaction.",
                    voiceEvent.EventType,
                    voiceEvent.ProviderName,
                    voiceEvent.CallId);
            }

            return;
        }

        if (interaction.Outcome != CallOutcome.InProgress)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Ignored Asterisk real-time event {EventType} for provider {ProviderName} call {CallId} because telephony interaction {InteractionId} is already terminal.",
                    voiceEvent.EventType,
                    voiceEvent.ProviderName,
                    voiceEvent.CallId,
                    interaction.InteractionId);
            }

            return;
        }

        ApplyInteractionState(interaction, voiceEvent);
        await _telephonyInteractionStore.UpdateAsync(interaction, cancellationToken);
        await _hubContext.Clients
            .Group(TenantSignalRGroupName.ForUser(_tenantName, interaction.UserId))
            .CallStateChanged(BuildTelephonyCall(interaction, voiceEvent));

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Projected Asterisk real-time event {EventType} for provider {ProviderName} call {CallId} to soft-phone user {UserId} as state {State}.",
                voiceEvent.EventType,
                voiceEvent.ProviderName,
                voiceEvent.CallId,
                interaction.UserId,
                voiceEvent.State);
        }
    }

    private static ProviderVoiceEvent BuildProviderVoiceEvent(AsteriskRealtimeVoiceEvent voiceEvent)
    {
        return new ProviderVoiceEvent
        {
            ProviderName = voiceEvent.ProviderName,
            ProviderCallId = voiceEvent.CallId,
            State = ToContactCenterState(voiceEvent.State),
            FromAddress = voiceEvent.FromAddress,
            ToAddress = voiceEvent.ToAddress,
            OccurredUtc = voiceEvent.OccurredUtc,
            IdempotencyKey = voiceEvent.IdempotencyKey,
            IsMuted = voiceEvent.IsMuted,
            IsConference = voiceEvent.IsConference,
            ParticipantCount = voiceEvent.ParticipantCount,
            Metadata = new Dictionary<string, string>(voiceEvent.Metadata, StringComparer.OrdinalIgnoreCase),
        };
    }

    private void ApplyInteractionState(TelephonyInteraction interaction, AsteriskRealtimeVoiceEvent voiceEvent)
    {
        var now = voiceEvent.OccurredUtc ?? _clock.UtcNow;

        interaction.ProviderName = voiceEvent.ProviderName;
        interaction.StartedUtc = interaction.StartedUtc == default ? now : interaction.StartedUtc;

        if (!string.IsNullOrWhiteSpace(voiceEvent.FromAddress))
        {
            interaction.From = voiceEvent.FromAddress;
        }

        if (!string.IsNullOrWhiteSpace(voiceEvent.ToAddress))
        {
            interaction.To = voiceEvent.ToAddress;
        }

        if (voiceEvent.State is CallState.Disconnected or CallState.Failed)
        {
            interaction.EndedUtc = now;
            interaction.DurationSeconds = Math.Max(0, (interaction.EndedUtc.Value - interaction.StartedUtc).TotalSeconds);
            interaction.Outcome = voiceEvent.State == CallState.Failed
                ? CallOutcome.Failed
                : CallOutcome.Completed;

            return;
        }

        interaction.EndedUtc = null;
        interaction.DurationSeconds = 0;
        interaction.Outcome = CallOutcome.InProgress;
    }

    private static ContactCenterCallState ToContactCenterState(CallState state)
    {
        return state switch
        {
            CallState.Connecting => ContactCenterCallState.Dialing,
            CallState.Ringing => ContactCenterCallState.Ringing,
            CallState.Connected => ContactCenterCallState.Connected,
            CallState.OnHold => ContactCenterCallState.OnHold,
            CallState.Disconnected => ContactCenterCallState.Ended,
            CallState.Failed => ContactCenterCallState.Failed,
            _ => ContactCenterCallState.Dialing,
        };
    }

    private static TelephonyCall BuildTelephonyCall(TelephonyInteraction interaction, AsteriskRealtimeVoiceEvent voiceEvent)
    {
        var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in voiceEvent.Metadata)
        {
            metadata[item.Key] = item.Value;
        }

        if (voiceEvent.IsConference.HasValue)
        {
            metadata["isConference"] = voiceEvent.IsConference.Value;
        }

        if (voiceEvent.ParticipantCount.HasValue)
        {
            metadata["participantCount"] = voiceEvent.ParticipantCount.Value;
        }

        return new TelephonyCall
        {
            CallId = interaction.CallId,
            From = interaction.From,
            To = interaction.To,
            State = voiceEvent.State,
            Direction = interaction.Direction,
            IsMuted = voiceEvent.IsMuted ?? false,
            IsOnHold = voiceEvent.IsOnHold,
            ProviderName = interaction.ProviderName,
            StartedUtc = interaction.StartedUtc == default
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(interaction.StartedUtc, DateTimeKind.Utc)),
            Metadata = metadata,
        };
    }
}
