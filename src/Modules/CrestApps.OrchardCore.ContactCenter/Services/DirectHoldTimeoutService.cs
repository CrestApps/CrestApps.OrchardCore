using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Sweeps direct-to-agent (personal line) calls that are held waiting under the synthetic direct-routing
/// queue. A held call rings and waits for its named agent; this bounds that wait: once the entry point's ring
/// window elapses the caller is sent to voicemail, while an entry point that disabled voicemail (ring window 0)
/// keeps the call held and re-offers it to the agent whenever they are available.
/// </summary>
public sealed class DirectHoldTimeoutService : IDirectHoldTimeoutService
{
    private readonly IQueueItemManager _queueItemManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IInboundVoiceCallProcessor _processor;
    private readonly IInboundVoiceService _inboundVoiceService;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DirectHoldTimeoutService"/> class.
    /// </summary>
    /// <param name="queueItemManager">The queue item manager used to find held direct calls.</param>
    /// <param name="interactionManager">The interaction manager used to resolve a held call's target agent and ring window.</param>
    /// <param name="processor">The inbound processor used to time a held call out to voicemail.</param>
    /// <param name="inboundVoiceService">The inbound voice service used to re-offer a held call whose entry point disabled voicemail.</param>
    /// <param name="session">The YesSql session used to commit each handled call.</param>
    /// <param name="clock">The clock used to evaluate ring windows.</param>
    /// <param name="logger">The logger.</param>
    public DirectHoldTimeoutService(
        IQueueItemManager queueItemManager,
        IInteractionManager interactionManager,
        IInboundVoiceCallProcessor processor,
        IInboundVoiceService inboundVoiceService,
        ISession session,
        IClock clock,
        ILogger<DirectHoldTimeoutService> logger)
    {
        _queueItemManager = queueItemManager;
        _interactionManager = interactionManager;
        _processor = processor;
        _inboundVoiceService = inboundVoiceService;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken = default)
    {
        var waiting = await _queueItemManager.GetWaitingAsync(ContactCenterConstants.DirectRouting.QueueId, cancellationToken);

        if (waiting.Count == 0)
        {
            return 0;
        }

        var now = _clock.UtcNow;
        var handled = 0;

        foreach (var item in waiting)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var interaction = await _interactionManager.FindByActivityIdAsync(item.ActivityItemId, cancellationToken);

            if (interaction is null)
            {
                continue;
            }

            var ringSeconds = ReadRingTimeoutSeconds(interaction) ?? ContactCenterConstants.DirectRouting.DefaultRingTimeoutSeconds;

            try
            {
                if (ringSeconds > 0)
                {
                    // Voicemail enabled: send the caller to voicemail once the ring window has elapsed.
                    if (now < item.EnqueuedUtc.AddSeconds(ringSeconds))
                    {
                        continue;
                    }

                    if (await _processor.TimeoutDirectHoldAsync(item.ActivityItemId, cancellationToken))
                    {
                        handled++;
                        await _session.SaveChangesAsync(cancellationToken);

                        if (_logger.IsEnabled(LogLevel.Information))
                        {
                            _logger.LogInformation(
                                "Sent held direct-to-agent call for activity '{ActivityItemId}' to voicemail after its {RingSeconds}s ring window elapsed.",
                                item.ActivityItemId.SanitizeLogValue(),
                                ringSeconds);
                        }
                    }
                }
                else
                {
                    // Voicemail disabled: keep trying to connect the caller to their named agent whenever the
                    // agent is available, instead of ever giving up on the call.
                    var targetAgentId = ReadTargetAgentId(interaction);

                    if (string.IsNullOrEmpty(targetAgentId))
                    {
                        continue;
                    }

                    var offered = await _inboundVoiceService.OfferToAgentAsync(
                        item.ActivityItemId,
                        ContactCenterConstants.DirectRouting.QueueId,
                        targetAgentId,
                        ringSeconds,
                        cancellationToken);

                    if (!string.IsNullOrWhiteSpace(offered))
                    {
                        handled++;
                        await _session.SaveChangesAsync(cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (ConcurrencyException)
            {
                // Another node handled this held call concurrently; skip it and let the next sweep reconcile.
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "An error occurred while processing the held direct-to-agent call for activity '{ActivityItemId}'.",
                    item.ActivityItemId.SanitizeLogValue());
            }
        }

        return handled;
    }

    private static int? ReadRingTimeoutSeconds(Interaction interaction)
    {
        if (interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.DirectRouting.RingTimeoutMetadataKey, out var value) &&
            value is not null &&
            int.TryParse(value.ToString(), out var seconds))
        {
            return seconds;
        }

        return null;
    }

    private static string ReadTargetAgentId(Interaction interaction)
    {
        return interaction.TechnicalMetadata.TryGetValue(ContactCenterConstants.DirectRouting.TargetAgentMetadataKey, out var value)
            ? value?.ToString()
            : null;
    }
}
