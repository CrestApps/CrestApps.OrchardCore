using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Reconciles active Contact Center interactions with the current provider call state so stale offers and
/// restart windows do not drift away from the telephony server truth.
/// </summary>
public sealed class ProviderCallStateSynchronizationService : IProviderCallStateSynchronizationService
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(2);
    private const string ReconciliationLockKey = "ContactCenterProviderCallStateReconciliation";

    private readonly IInteractionManager _interactionManager;
    private readonly ICallSessionManager _callSessionManager;
    private readonly IProviderVoiceEventService _providerVoiceEventService;
    private readonly ITelephonyProviderResolver _telephonyProviderResolver;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderCallStateSynchronizationService"/> class.
    /// </summary>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="callSessionManager">The call session manager.</param>
    /// <param name="providerVoiceEventService">The provider voice-event ingestion service.</param>
    /// <param name="telephonyProviderResolver">The telephony provider resolver.</param>
    /// <param name="distributedLock">The distributed lock used to prevent overlapping reconciliation sweeps.</param>
    /// <param name="clock">The clock used to stamp reconciliation events.</param>
    /// <param name="logger">The logger.</param>
    public ProviderCallStateSynchronizationService(
        IInteractionManager interactionManager,
        ICallSessionManager callSessionManager,
        IProviderVoiceEventService providerVoiceEventService,
        ITelephonyProviderResolver telephonyProviderResolver,
        IDistributedLock distributedLock,
        IClock clock,
        ILogger<ProviderCallStateSynchronizationService> logger)
    {
        _interactionManager = interactionManager;
        _callSessionManager = callSessionManager;
        _providerVoiceEventService = providerVoiceEventService;
        _telephonyProviderResolver = telephonyProviderResolver;
        _distributedLock = distributedLock;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Interaction> RefreshInteractionAsync(Interaction interaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        if (string.IsNullOrWhiteSpace(interaction.ProviderName) || string.IsNullOrWhiteSpace(interaction.ProviderInteractionId))
        {
            return interaction;
        }

        var provider = await _telephonyProviderResolver.GetAsync(interaction.ProviderName);

        if (provider is not ITelephonyCallStateProvider stateProvider)
        {
            return interaction;
        }

        var lookup = await stateProvider.GetCallStateAsync(interaction.ProviderInteractionId, cancellationToken);

        if (!lookup.Succeeded)
        {
            if (_logger.IsEnabled(LogLevel.Warning))
            {
                _logger.LogWarning(
                    "Skipping provider-state reconciliation for interaction '{InteractionId}' because provider '{Provider}' could not resolve call '{ProviderCallId}': {ErrorMessage}",
                    interaction.ItemId,
                    interaction.ProviderName,
                    interaction.ProviderInteractionId,
                    lookup.Error);
            }

            return interaction;
        }

        var currentSession = await _callSessionManager.FindByInteractionIdAsync(interaction.ItemId, cancellationToken);

        if (lookup.Found && IsEquivalent(currentSession, lookup.Call))
        {
            return interaction;
        }

        var providerEvent = BuildProviderEvent(interaction, lookup);
        await _providerVoiceEventService.IngestAsync(providerEvent, cancellationToken);

        return await _interactionManager.FindByProviderInteractionIdAsync(
            interaction.ProviderName,
            interaction.ProviderInteractionId,
            cancellationToken)
            ?? interaction;
    }

    /// <inheritdoc/>
    public async Task<int> ReconcileActiveInteractionsAsync(CancellationToken cancellationToken = default)
    {
        return await ReconcileAsync(providerName: null, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> ReconcileProviderInteractionsAsync(string providerName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);

        return await ReconcileAsync(providerName, cancellationToken);
    }

    private async Task<int> ReconcileAsync(string providerName, CancellationToken cancellationToken)
    {
        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            ReconciliationLockKey,
            _lockTimeout,
            _lockExpiration);

        if (!locked)
        {
            return 0;
        }

        await using var acquiredLock = locker;

        var interactions = string.IsNullOrEmpty(providerName)
            ? await _interactionManager.ListActiveWithProviderCallIdAsync(cancellationToken)
            : await _interactionManager.ListActiveWithProviderCallIdAsync(providerName, cancellationToken);
        var refreshed = 0;

        foreach (var interaction in interactions)
        {
            var currentStatus = interaction.Status;
            var updated = await RefreshInteractionAsync(interaction, cancellationToken);

            if (updated.Status != currentStatus)
            {
                refreshed++;
            }
        }

        return refreshed;
    }

    private ProviderVoiceEvent BuildProviderEvent(Interaction interaction, TelephonyCallLookupResult lookup)
    {
        if (!lookup.Found)
        {
            return new ProviderVoiceEvent
            {
                ProviderName = interaction.ProviderName,
                ProviderCallId = interaction.ProviderInteractionId,
                State = ContactCenterCallState.Ended,
                OccurredUtc = _clock.UtcNow,
                IdempotencyKey = $"reconcile-missing:{interaction.ProviderName}:{interaction.ProviderInteractionId}:ended",
            };
        }

        var call = lookup.Call;
        var state = call.State switch
        {
            CallState.Connecting => ContactCenterCallState.Dialing,
            CallState.Ringing => ContactCenterCallState.Ringing,
            CallState.Connected when call.IsOnHold => ContactCenterCallState.OnHold,
            CallState.Connected => ContactCenterCallState.Connected,
            CallState.OnHold => ContactCenterCallState.OnHold,
            CallState.Disconnected => ContactCenterCallState.Ended,
            CallState.Failed => ContactCenterCallState.Failed,
            _ => ContactCenterCallState.Ended,
        };

        return new ProviderVoiceEvent
        {
            ProviderName = interaction.ProviderName,
            ProviderCallId = interaction.ProviderInteractionId,
            State = state,
            FromAddress = call.From,
            ToAddress = call.To,
            OccurredUtc = _clock.UtcNow,
            IdempotencyKey = $"reconcile:{interaction.ProviderName}:{interaction.ProviderInteractionId}:{state}:{call.IsMuted}:{call.IsOnHold}",
            IsMuted = call.IsMuted,
            Metadata = call.Metadata?
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Key))
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value?.ToString() ?? string.Empty,
                    StringComparer.OrdinalIgnoreCase) ?? [],
        };
    }

    private static bool IsEquivalent(CallSession session, TelephonyCall call)
    {
        if (session is null || call is null)
        {
            return false;
        }

        var mappedState = call.State switch
        {
            CallState.Connecting => ContactCenterCallState.Dialing,
            CallState.Ringing => ContactCenterCallState.Ringing,
            CallState.Connected when call.IsOnHold => ContactCenterCallState.OnHold,
            CallState.Connected => ContactCenterCallState.Connected,
            CallState.OnHold => ContactCenterCallState.OnHold,
            CallState.Disconnected => ContactCenterCallState.Ended,
            CallState.Failed => ContactCenterCallState.Failed,
            _ => ContactCenterCallState.Ended,
        };

        return session.State == mappedState &&
            session.IsMuted == call.IsMuted &&
            session.IsOnHold == (mappedState == ContactCenterCallState.OnHold);
    }
}
