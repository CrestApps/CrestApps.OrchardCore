using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Tells whoever keeps a record of automated calls -- the Contact Center's audit log, when it is enabled -- that a
/// call was answered, who answered it, and how the conversation ended.
/// </summary>
public sealed partial class VoiceAgentConversationLoop
{
    private const string HandedToAgentOutcome = "HandedToAgent";
    private const string NotConcludedOutcome = "NotConcluded";
    private const string CompletedOutcome = "Completed";
    private const string VoicemailOutcome = "Voicemail";

    private Task ObserveAsync(
        VoiceAgentEvent voiceEvent,
        AutomatedVoiceCallObservationKind kind,
        string outcome = null,
        string dispositionId = null,
        CancellationToken cancellationToken = default)
        => NotifyAsync(
            _callObservers,
            CreateObservation(voiceEvent, kind, _clock.UtcNow, outcome, dispositionId),
            _logger,
            cancellationToken);

    /// <summary>
    /// Reports the end of a conversation that was concluded, once the conclusion has decided its disposition.
    /// Runs in the deferred scope the conclusion ran in, because the request that ended the call is gone.
    /// </summary>
    private static async Task ObserveConclusionAsync(IServiceProvider services, VoiceAgentEvent voiceEvent, DateTime endedUtc)
    {
        var observers = services.GetServices<IAutomatedVoiceCallObserver>();

        if (!observers.Any())
        {
            return;
        }

        var activity = await services.GetRequiredService<IOmnichannelActivityStore>().FindByIdAsync(voiceEvent.ActivityId);

        var outcome = activity is null || activity.Status != ActivityStatus.Completed
            ? NotConcludedOutcome
            : activity.TryGet<VoicemailReached>(out _) ? VoicemailOutcome : CompletedOutcome;

        await NotifyAsync(
            observers,
            CreateObservation(voiceEvent, AutomatedVoiceCallObservationKind.ConversationEnded, endedUtc, outcome, activity?.DispositionId),
            services.GetRequiredService<ILogger<VoiceAgentConversationLoop>>(),
            CancellationToken.None);
    }

    private static AutomatedVoiceCallObservation CreateObservation(
        VoiceAgentEvent voiceEvent,
        AutomatedVoiceCallObservationKind kind,
        DateTime occurredUtc,
        string outcome,
        string dispositionId)
        => new()
        {
            Kind = kind,
            ActivityItemId = voiceEvent.ActivityId,
            ProviderName = voiceEvent.ProviderName,
            ProviderCallId = voiceEvent.ProviderCallId,
            OccurredUtc = occurredUtc,
            Answerer = kind == AutomatedVoiceCallObservationKind.AnswererDetected ? voiceEvent.Answerer.ToString() : null,
            Outcome = outcome,
            DispositionId = dispositionId,
        };

    // A record of the call must never cost the call: each observer is told on its own, and one that fails is
    // logged rather than allowed to stop the conversation or the observers after it.
    private static async Task NotifyAsync(
        IEnumerable<IAutomatedVoiceCallObserver> observers,
        AutomatedVoiceCallObservation observation,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        foreach (var observer in observers)
        {
            try
            {
                await observer.ObserveAsync(observation, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "An automated call observer could not record the '{Kind}' moment of AI voice activity '{ActivityId}'.",
                    observation.Kind,
                    observation.ActivityItemId.SanitizeLogValue());
            }
        }
    }
}
