using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="ICallRecordingCatalog"/>.
/// </summary>
public sealed class CallRecordingCatalog : ICallRecordingCatalog
{
    private readonly ICallRecordingStore _store;
    private readonly IInteractionManager _interactionManager;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallRecordingCatalog"/> class.
    /// </summary>
    /// <param name="store">The call recordings store.</param>
    /// <param name="interactionManager">The interaction manager, read to describe a recorded interaction.</param>
    /// <param name="agentProfileManagers">The agent profile manager, when agents are enabled, used to name the interaction's agent by user.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public CallRecordingCatalog(
        ICallRecordingStore store,
        IInteractionManager interactionManager,
        IEnumerable<IAgentProfileManager> agentProfileManagers,
        IClock clock,
        ILogger<CallRecordingCatalog> logger)
    {
        _store = store;
        _interactionManager = interactionManager;
        _agentProfileManager = agentProfileManagers.FirstOrDefault();
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CallRecording> BeginAsync(CallRecordingRegistration registration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentException.ThrowIfNullOrEmpty(registration.ProviderCallId);

        var recording = await CreateAsync(registration, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Listed the recording started on call leg {ProviderCallId} ({Source}) for interaction {InteractionId}, activity {ActivityItemId}, agent {AgentUserId}; it is completed when the provider saves it.",
                registration.ProviderCallId.SanitizeLogValue(),
                recording.Source,
                recording.InteractionId.SanitizeLogValue(),
                recording.ActivityItemId.SanitizeLogValue(),
                recording.AgentUserId.SanitizeLogValue());
        }

        return recording;
    }

    /// <inheritdoc/>
    public Task<CallRecording> FindRunningAsync(string providerCallId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(providerCallId)
            ? Task.FromResult<CallRecording>(null)
            : _store.FindRunningByProviderCallIdAsync(providerCallId, cancellationToken);

    /// <inheritdoc/>
    public async Task<CallRecording> RegisterAsync(CallRecordingRegistration registration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentException.ThrowIfNullOrEmpty(registration.ProviderRecordingId);

        var existing = await _store.FindByProviderRecordingIdAsync(registration.ProviderRecordingId, cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        // The platform listed this recording when it started it; the provider has now saved it.
        var running = string.IsNullOrEmpty(registration.ProviderCallId)
            ? null
            : await _store.FindRunningByProviderCallIdAsync(registration.ProviderCallId, cancellationToken);

        if (running is not null)
        {
            running.ProviderRecordingId = registration.ProviderRecordingId;
            running.StorageReference = registration.StorageReference ?? registration.ProviderRecordingId;
            running.Format = registration.Format ?? running.Format;
            running.StartedUtc = registration.StartedUtc ?? running.StartedUtc;
            running.EndedUtc = registration.EndedUtc ?? running.EndedUtc;
            running.DurationSeconds = DurationOf(running);

            if (string.IsNullOrEmpty(running.InteractionId) && !string.IsNullOrEmpty(registration.InteractionId))
            {
                running.InteractionId = registration.InteractionId;
                await DescribeFromInteractionAsync(running, registration, cancellationToken);
            }

            await _store.UpdateAsync(running, cancellationToken);

            LogListed(running);

            return running;
        }

        var recording = await CreateAsync(registration, cancellationToken);

        LogListed(recording);

        return recording;
    }

    private async Task<CallRecording> CreateAsync(CallRecordingRegistration registration, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var recording = new CallRecording
        {
            ItemId = IdGenerator.GenerateId(),
            Source = registration.Source,
            ProviderName = registration.ProviderName,
            ProviderRecordingId = registration.ProviderRecordingId,
            ProviderCallId = registration.ProviderCallId,
            StorageReference = registration.StorageReference ?? registration.ProviderRecordingId,
            Format = registration.Format,
            InteractionId = registration.InteractionId,
            ActivityItemId = registration.ActivityItemId,
            AiSessionId = registration.AiSessionId,
            TelephonyCallId = registration.TelephonyCallId,
            AgentUserId = registration.AgentUserId,
            CustomerAddress = registration.CustomerAddress,
            Direction = registration.Direction ?? default,
            StartedUtc = registration.StartedUtc ?? now,
            EndedUtc = registration.EndedUtc,
            CreatedUtc = now,
        };

        if (!string.IsNullOrEmpty(registration.InteractionId))
        {
            await DescribeFromInteractionAsync(recording, registration, cancellationToken);
        }

        recording.DurationSeconds = DurationOf(recording);

        await _store.CreateAsync(recording, cancellationToken);

        return recording;
    }

    private static double DurationOf(CallRecording recording)
        => recording.EndedUtc is { } endedUtc && endedUtc > recording.StartedUtc
            ? Math.Round((endedUtc - recording.StartedUtc).TotalSeconds, 1)
            : 0;

    private void LogListed(CallRecording recording)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Listed call recording {RecordingId} ({Source}) for interaction {InteractionId}, activity {ActivityItemId}, agent {AgentUserId}; it plays once it is stored.",
                recording.ProviderRecordingId.SanitizeLogValue(),
                recording.Source,
                recording.InteractionId.SanitizeLogValue(),
                recording.ActivityItemId.SanitizeLogValue(),
                recording.AgentUserId.SanitizeLogValue());
        }
    }

    /// <inheritdoc/>
    public async Task<bool> MarkStoredAsync(string providerRecordingId, string storageReference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(providerRecordingId))
        {
            return false;
        }

        var recording = await _store.FindByProviderRecordingIdAsync(providerRecordingId, cancellationToken);

        if (recording is null)
        {
            return false;
        }

        if (recording.StoredUtc.HasValue && string.Equals(recording.StorageReference, storageReference, StringComparison.Ordinal))
        {
            return true;
        }

        recording.StoredUtc = _clock.UtcNow;

        if (!string.IsNullOrEmpty(storageReference))
        {
            recording.StorageReference = storageReference;
        }

        await _store.UpdateAsync(recording, cancellationToken);

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> IsErasedAsync(string providerRecordingId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(providerRecordingId))
        {
            return true;
        }

        var recording = await _store.FindByProviderRecordingIdAsync(providerRecordingId, cancellationToken);

        return recording is null || recording.ErasedUtc.HasValue;
    }

    private async Task DescribeFromInteractionAsync(CallRecording recording, CallRecordingRegistration registration, CancellationToken cancellationToken)
    {
        var interaction = await _interactionManager.FindByIdAsync(registration.InteractionId, cancellationToken);

        if (interaction is null)
        {
            return;
        }

        recording.ActivityItemId ??= interaction.ActivityItemId;
        recording.CustomerAddress ??= interaction.CustomerAddress;

        if (registration.Direction is null)
        {
            recording.Direction = interaction.Direction;
        }

        // The interaction names its agent by agent profile; the page lists calls by user, which is who signs in.
        if (string.IsNullOrEmpty(recording.AgentUserId) &&
            !string.IsNullOrEmpty(interaction.AgentId) &&
            _agentProfileManager is not null)
        {
            var agent = await _agentProfileManager.FindByIdAsync(interaction.AgentId, cancellationToken);

            recording.AgentUserId = agent?.UserId;
        }

        // A handed-off AI call is the AI's conversation until the hand-off, so its transcript belongs on the page too.
        recording.AiSessionId ??= interaction.HandoffAiSessionId;
    }
}
