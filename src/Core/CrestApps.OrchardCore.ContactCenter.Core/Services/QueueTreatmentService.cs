using System.Globalization;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Between the policy that decides what a waiting caller is due to hear and the provider that makes it audible:
/// finds the caller's live leg, works out what there is to say, says it, and records that it was said.
/// </summary>
public sealed class QueueTreatmentService : IQueueTreatmentService
{
    private readonly IQueueItemManager _queueItemManager;
    private readonly IInteractionManager _interactionManager;
    private readonly IQueueTreatmentProvider _treatmentProvider;
    private readonly IAgentAvailabilityService _availabilityService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="QueueTreatmentService"/> class.
    /// </summary>
    /// <param name="queueItemManager">The queue item manager.</param>
    /// <param name="interactionManager">The interaction manager, which knows the caller's provider leg.</param>
    /// <param name="treatmentProvider">The provider that makes the caller hear it.</param>
    /// <param name="availabilityService">The availability service, for the estimate's divisor.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public QueueTreatmentService(
        IQueueItemManager queueItemManager,
        IInteractionManager interactionManager,
        IQueueTreatmentProvider treatmentProvider,
        IAgentAvailabilityService availabilityService,
        IClock clock,
        ILogger<QueueTreatmentService> logger)
    {
        _queueItemManager = queueItemManager;
        _interactionManager = interactionManager;
        _treatmentProvider = treatmentProvider;
        _availabilityService = availabilityService;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<int> RunDueAsync(ActivityQueue queue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queue);

        var settings = queue.Treatment;

        // This runs every few seconds against every queue, so a queue that configures no treatment must not cost
        // a read of everybody waiting in it.
        if (settings is null || !HasAnything(settings))
        {
            return 0;
        }

        var waiting = await _queueItemManager.GetWaitingAsync(queue.ItemId, cancellationToken);

        if (waiting.Count == 0)
        {
            return 0;
        }

        var now = _clock.UtcNow;
        var availableAgents = settings.AnnounceEstimatedWait
            ? (await _availabilityService.GetForQueueAsync(queue.ItemId, cancellationToken)).Count
            : 0;

        var treated = 0;
        var position = 0;

        foreach (var item in waiting)
        {
            position++;

            var step = QueueTreatmentPolicy.GetNextStep(item, settings, now);

            if (step.Kind == QueueTreatmentStepKind.None)
            {
                continue;
            }

            var interaction = await _interactionManager.FindByActivityIdAsync(item.ActivityItemId, cancellationToken);
            var providerCallId = interaction?.ProviderInteractionId;

            // A queued call that has not been answered yet, or has already gone, has nothing to speak on.
            // Recording it as treated would silently consume the welcome this caller never heard.
            if (string.IsNullOrEmpty(providerCallId))
            {
                continue;
            }

            try
            {
                if (!await PlayAsync(step, item, settings, providerCallId, position, availableAgents, now, cancellationToken))
                {
                    continue;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // One caller who hung up between the read and the command must not end the pass and leave every
                // other caller in this queue in silence.
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(ex, "Queue treatment could not be played to waiting item '{ItemId}'.", item.ItemId.SanitizeLogValue());
                }

                continue;
            }

            item.TreatmentStepsPlayed++;
            item.LastTreatmentUtc = now;
            await _queueItemManager.UpdateAsync(item, cancellationToken: cancellationToken);

            treated++;
        }

        return treated;
    }

    /// <summary>
    /// Plays one step, and says whether the caller actually heard anything.
    /// </summary>
    private async Task<bool> PlayAsync(
        QueueTreatmentStep step,
        QueueItem item,
        QueueTreatmentSettings settings,
        string providerCallId,
        int position,
        int availableAgents,
        DateTime now,
        CancellationToken cancellationToken)
    {
        switch (step.Kind)
        {
            case QueueTreatmentStepKind.Welcome:
                await _treatmentProvider.SpeakAsync(providerCallId, step.Text, cancellationToken);

                // Music starts behind the welcome, so the caller is not left in silence until the first update.
                await _treatmentProvider.StartHoldMusicAsync(providerCallId, settings.HoldMusicMediaId, cancellationToken);

                return true;

            case QueueTreatmentStepKind.HoldMusic:
                await _treatmentProvider.StartHoldMusicAsync(providerCallId, settings.HoldMusicMediaId, cancellationToken);

                return true;

            case QueueTreatmentStepKind.CallbackOffer:
                await _treatmentProvider.OfferChoiceAsync(
                    providerCallId,
                    BuildCallbackPrompt(step.DtmfKey),
                    step.DtmfKey,
                    cancellationToken);

                // Recorded so the offer is made once. Re-prompting somebody who already declined, every few
                // seconds for the rest of their wait, is worse than never offering.
                item.CallbackOfferedUtc = now;

                return true;

            case QueueTreatmentStepKind.Announcement:
                var announcement = BuildAnnouncement(settings, position, availableAgents);

                // A queue that announces neither position nor wait has configured a cadence with no content.
                // Speaking an empty sentence interrupts the hold music for nothing.
                if (string.IsNullOrEmpty(announcement))
                {
                    return false;
                }

                await _treatmentProvider.SpeakAsync(providerCallId, announcement, cancellationToken);

                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Whether this queue has asked for its callers to hear anything at all. A queue that has not is skipped
    /// before anybody waiting in it is read, because this runs constantly against every queue.
    /// </summary>
    /// <remarks>
    /// Hold music counts. It was missing here, which meant a queue whose only treatment was music — the most
    /// ordinary configuration there is — was classed as having nothing configured and skipped before its callers
    /// were ever looked at. The music was set, the provider could play it, and the caller sat in silence.
    /// </remarks>
    private static bool HasAnything(QueueTreatmentSettings settings)
        => !string.IsNullOrWhiteSpace(settings.WelcomeMessage)
            || !string.IsNullOrWhiteSpace(settings.HoldMusicMediaId)
            || !string.IsNullOrWhiteSpace(settings.CallbackDtmfKey)
            || (settings.AnnouncementIntervalSeconds > 0 && (settings.AnnouncePosition || settings.AnnounceEstimatedWait));

    private static string BuildCallbackPrompt(string acceptKey)
        => string.Create(
            CultureInfo.InvariantCulture,
            $"If you would rather not wait, press {acceptKey} and we will call you back without losing your place in line.");

    private static string BuildAnnouncement(QueueTreatmentSettings settings, int position, int availableAgents)
    {
        var parts = new List<string>(2);

        if (settings.AnnouncePosition)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"You are number {position} in line."));
        }

        if (settings.AnnounceEstimatedWait)
        {
            var estimate = EstimatedWaitTimeCalculator.Estimate(
                position,
                availableAgents,
                TimeSpan.FromSeconds(Math.Max(0, settings.AverageHandleTimeSeconds)),
                settings);

            // No estimate rather than a wrong one: a queue nobody is working is not moving, and a number
            // invented to fill the sentence is worse than saying nothing about the wait.
            if (estimate is not null)
            {
                var minutes = Math.Max(1, (int)Math.Round(estimate.Value.TotalMinutes, MidpointRounding.AwayFromZero));

                parts.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Your estimated wait is about {minutes} {(minutes == 1 ? "minute" : "minutes")}."));
            }
        }

        return parts.Count == 0 ? null : string.Join(' ', parts);
    }
}
