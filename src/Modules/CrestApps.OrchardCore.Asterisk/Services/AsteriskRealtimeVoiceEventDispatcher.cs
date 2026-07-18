using CrestApps.OrchardCore.Diagnostics;
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
    private readonly IEnumerable<IAsteriskRealtimeVoiceEventBridge> _voiceEventBridges;
    private readonly IEnumerable<IAsteriskCallTeardownService> _callTeardownServices;
    private readonly ITelephonyInteractionStore _telephonyInteractionStore;
    private readonly IHubContext<TelephonyHub, ITelephonyClient> _hubContext;
    private readonly IClock _clock;
    private readonly ILogger<AsteriskRealtimeVoiceEventDispatcher> _logger;
    private readonly string _tenantName;

    public AsteriskRealtimeVoiceEventDispatcher(
        IEnumerable<IAsteriskRealtimeVoiceEventBridge> voiceEventBridges,
        IEnumerable<IAsteriskCallTeardownService> callTeardownServices,
        ITelephonyInteractionStore telephonyInteractionStore,
        IHubContext<TelephonyHub, ITelephonyClient> hubContext,
        IClock clock,
        ILogger<AsteriskRealtimeVoiceEventDispatcher> logger,
        ShellSettings shellSettings)
    {
        _voiceEventBridges = voiceEventBridges;
        _callTeardownServices = callTeardownServices;
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

        var handledByBridge = false;

        try
        {
            foreach (var voiceEventBridge in _voiceEventBridges)
            {
                if (await voiceEventBridge.TryHandleAsync(voiceEvent, cancellationToken))
                {
                    handledByBridge = true;

                    break;
                }
            }
        }
        finally
        {
            // Terminal resource cleanup runs in a finally so it happens regardless of which bridge (if any)
            // claimed the event and even if a bridge throws, because releasing ARI bridges, channels, and
            // ownership bindings is orthogonal to projecting call status. Each service is a no-op for
            // non-terminal events and for channels the current tenant does not own.
            foreach (var callTeardownService in _callTeardownServices)
            {
                await callTeardownService.ReleaseAsync(voiceEvent, cancellationToken);
            }
        }

        if (handledByBridge)
        {
            return;
        }

        var interaction = await _telephonyInteractionStore.FindByProviderCallIdAsync(voiceEvent.ProviderName, voiceEvent.CallId, cancellationToken);

        if (interaction is null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Asterisk real-time event {EventType} for provider {ProviderName} call {CallId} did not match any telephony interaction.",
                    OperationalLogRedactor.Redact(voiceEvent.EventType, OperationalLogFieldKind.FreeText),
                    voiceEvent.ProviderName,
                    OperationalLogRedactor.Pseudonymize(voiceEvent.CallId, OperationalLogIdentifierCategory.Call));
            }

            return;
        }

        if (interaction.Outcome != CallOutcome.InProgress)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Ignored Asterisk real-time event {EventType} for provider {ProviderName} call {CallId} because telephony interaction {InteractionId} is already terminal.",
                    OperationalLogRedactor.Redact(voiceEvent.EventType, OperationalLogFieldKind.FreeText),
                    voiceEvent.ProviderName,
                    OperationalLogRedactor.Pseudonymize(voiceEvent.CallId, OperationalLogIdentifierCategory.Call),
                    OperationalLogRedactor.Pseudonymize(interaction.InteractionId, OperationalLogIdentifierCategory.Interaction));
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
                OperationalLogRedactor.Redact(voiceEvent.EventType, OperationalLogFieldKind.FreeText),
                voiceEvent.ProviderName,
                OperationalLogRedactor.Pseudonymize(voiceEvent.CallId, OperationalLogIdentifierCategory.Call),
                OperationalLogRedactor.Pseudonymize(interaction.UserId, OperationalLogIdentifierCategory.User),
                voiceEvent.State);
        }
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
