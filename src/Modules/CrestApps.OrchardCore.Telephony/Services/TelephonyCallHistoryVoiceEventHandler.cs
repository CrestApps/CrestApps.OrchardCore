using CrestApps.Core.Support;
using CrestApps.OrchardCore.SignalR;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Hubs;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Projects the normalized provider voice event stream onto the telephony call history and the soft-phone hub.
/// <para>
/// This projection is a peer of every other normalized-event consumer rather than a fallback for them. It
/// previously ran only when no higher-level consumer claimed the event, which meant enabling Contact Center
/// silently stopped telephony call history from ever reaching a terminal outcome for calls Contact Center
/// owned, and left the soft phone showing a call the provider had already ended.
/// </para>
/// </summary>
public sealed class TelephonyCallHistoryVoiceEventHandler : INormalizedVoiceEventHandler
{
    private readonly ITelephonyInteractionStore _telephonyInteractionStore;
    private readonly IHubContext<TelephonyHub, ITelephonyClient> _hubContext;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelephonyCallHistoryVoiceEventHandler"/> class.
    /// </summary>
    /// <param name="telephonyInteractionStore">The telephony interaction store that owns call history.</param>
    /// <param name="hubContext">The soft-phone hub context.</param>
    /// <param name="clock">The clock used to stamp call history times.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="shellSettings">The tenant shell settings used to scope hub groups.</param>
    public TelephonyCallHistoryVoiceEventHandler(
        ITelephonyInteractionStore telephonyInteractionStore,
        IHubContext<TelephonyHub, ITelephonyClient> hubContext,
        IClock clock,
        ILogger<TelephonyCallHistoryVoiceEventHandler> logger,
        ShellSettings shellSettings)
    {
        _telephonyInteractionStore = telephonyInteractionStore;
        _hubContext = hubContext;
        _clock = clock;
        _logger = logger;
        _tenantName = shellSettings.Name;
    }

    /// <inheritdoc/>
    public int Order => 0;

    /// <inheritdoc/>
    public async Task<bool> HandleAsync(
        ProviderVoiceEvent providerEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerEvent);

        if (string.IsNullOrWhiteSpace(providerEvent.ProviderName) ||
            string.IsNullOrWhiteSpace(providerEvent.ProviderCallId))
        {
            return false;
        }

        var alreadyTerminal = false;
        var interaction = await _telephonyInteractionStore.UpdateByProviderCallIdAsync(
            providerEvent.ProviderName,
            providerEvent.ProviderCallId,
            candidate =>
            {
                // The terminal check has to run against the version this attempt actually read. Evaluating it
                // on a copy loaded earlier would let a hangup that committed in between be overwritten by a
                // stale state. It is also the projection's own replay guard: a redelivered terminal event
                // finds the interaction already terminal and writes nothing, so this projection never needs a
                // de-duplication record of its own.
                alreadyTerminal = candidate.Outcome != CallOutcome.InProgress;

                if (alreadyTerminal)
                {
                    return false;
                }

                ApplyInteractionState(candidate, providerEvent);

                return true;
            },
            cancellationToken);

        if (interaction is null)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Normalized voice event for provider {ProviderName} call {CallId} did not match any telephony interaction.",
                    providerEvent.ProviderName,
                    providerEvent.ProviderCallId.SanitizeLogValue());
            }

            return false;
        }

        if (alreadyTerminal)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Ignored normalized voice event for provider {ProviderName} call {CallId} because telephony interaction {InteractionId} is already terminal.",
                    providerEvent.ProviderName,
                    providerEvent.ProviderCallId.SanitizeLogValue(),
                    interaction.InteractionId.SanitizeLogValue());
            }

            return false;
        }

        await _hubContext.Clients
            .Group(TenantSignalRGroupName.ForUser(_tenantName, interaction.UserId))
            .CallStateChanged(BuildTelephonyCall(interaction, providerEvent));

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Projected normalized voice event for provider {ProviderName} call {CallId} to soft-phone user {UserId} as state {State}.",
                providerEvent.ProviderName,
                providerEvent.ProviderCallId.SanitizeLogValue(),
                interaction.UserId.SanitizeLogValue(),
                providerEvent.State);
        }

        return true;
    }

    private void ApplyInteractionState(TelephonyInteraction interaction, ProviderVoiceEvent providerEvent)
    {
        var now = providerEvent.OccurredUtc ?? _clock.UtcNow;
        var state = VoiceCallStateProjection.ToTelephonyCallState(providerEvent.State);

        interaction.ProviderName = providerEvent.ProviderName;
        interaction.StartedUtc = interaction.StartedUtc == default ? now : interaction.StartedUtc;

        if (!string.IsNullOrWhiteSpace(providerEvent.FromAddress))
        {
            interaction.From = providerEvent.FromAddress;
        }

        if (!string.IsNullOrWhiteSpace(providerEvent.ToAddress))
        {
            interaction.To = providerEvent.ToAddress;
        }

        if (state is CallState.Disconnected or CallState.Failed)
        {
            interaction.EndedUtc = now;
            interaction.DurationSeconds = Math.Max(0, (interaction.EndedUtc.Value - interaction.StartedUtc).TotalSeconds);
            interaction.Outcome = state == CallState.Failed
                ? CallOutcome.Failed
                : CallOutcome.Completed;

            return;
        }

        interaction.EndedUtc = null;
        interaction.DurationSeconds = 0;
        interaction.Outcome = CallOutcome.InProgress;
    }

    private static TelephonyCall BuildTelephonyCall(TelephonyInteraction interaction, ProviderVoiceEvent providerEvent)
    {
        var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in providerEvent.Metadata)
        {
            metadata[item.Key] = item.Value;
        }

        if (providerEvent.IsConference.HasValue)
        {
            metadata["isConference"] = providerEvent.IsConference.Value;
        }

        if (providerEvent.ParticipantCount.HasValue)
        {
            metadata["participantCount"] = providerEvent.ParticipantCount.Value;
        }

        return new TelephonyCall
        {
            CallId = interaction.CallId,
            From = interaction.From,
            To = interaction.To,
            State = VoiceCallStateProjection.ToTelephonyCallState(providerEvent.State),
            Direction = interaction.Direction,
            IsMuted = providerEvent.IsMuted ?? false,
            IsOnHold = providerEvent.State == VoiceCallState.OnHold,
            ProviderName = interaction.ProviderName,
            StartedUtc = interaction.StartedUtc == default
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(interaction.StartedUtc, DateTimeKind.Utc)),
            Metadata = metadata,
        };
    }
}
