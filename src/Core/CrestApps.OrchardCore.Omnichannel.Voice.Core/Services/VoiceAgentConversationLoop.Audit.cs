using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using Microsoft.Extensions.AI;
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
        DateTime? occurredUtc = null,
        CancellationToken cancellationToken = default)
        => NotifyAsync(
            _callObservers,
            CreateObservation(voiceEvent, kind, occurredUtc ?? _clock.UtcNow, outcome, dispositionId),
            _logger,
            cancellationToken);

    /// <summary>
    /// Reports whether the person was told the call is recorded, so the notice is on the record with the words used,
    /// and a call that went without it is found rather than assumed to have had it.
    /// </summary>
    /// <param name="voiceEvent">The call's event.</param>
    /// <param name="disclosure">The disclosure the assistant was to give.</param>
    /// <param name="openingLine">What the assistant said first, or <see langword="null"/> when it never spoke.</param>
    /// <param name="said">Whether the disclosure was given.</param>
    /// <param name="occurredUtc">When the opening line was said, or when the call ended without one.</param>
    private Task ObserveRecordingDisclosureAsync(
        VoiceAgentEvent voiceEvent,
        string disclosure,
        string openingLine,
        bool said,
        DateTime occurredUtc)
    {
        var observation = CreateObservation(
            voiceEvent,
            said ? AutomatedVoiceCallObservationKind.RecordingDisclosed : AutomatedVoiceCallObservationKind.RecordingDisclosureMissed,
            occurredUtc,
            outcome: null,
            dispositionId: null);

        observation.RecordingDisclosure = disclosure;
        observation.OpeningLine = said ? null : openingLine;

        if (!said)
        {
            _logger.LogWarning(
                "The assistant on AI voice activity '{ActivityId}' did not give the recording disclosure word for word in its opening line.",
                voiceEvent.ActivityId.SanitizeLogValue());
        }

        // Told after the call, never during it, so the call is not held up; and with no token, because the request
        // that carried the call may already be gone.
        return NotifyAsync(_callObservers, observation, _logger, CancellationToken.None);
    }

    /// <summary>
    /// Checks a live session's opening line against the disclosure it was told to give, once the session is over.
    /// </summary>
    /// <param name="voiceEvent">The call's event.</param>
    /// <param name="sessionId">The chat session the transcript is stored in.</param>
    /// <param name="disclosure">The disclosure the assistant was to give.</param>
    private async Task VerifyRealtimeRecordingDisclosureAsync(VoiceAgentEvent voiceEvent, string sessionId, string disclosure)
    {
        try
        {
            var assistantLines = (await _promptStore.GetPromptsAsync(sessionId))
                .Where(prompt => prompt.Role == ChatRole.Assistant && !string.IsNullOrWhiteSpace(prompt.Content))
                .OrderBy(prompt => prompt.CreatedUtc)
                .ToList();

            var (said, line) = RecordingDisclosureCheck.FindInOpening(disclosure, assistantLines.Select(prompt => prompt.Content));

            // Dated by the line that gave it, or that moved on without it, rather than by when the call was checked.
            var occurredUtc = assistantLines.FirstOrDefault(prompt => ReferenceEquals(prompt.Content, line))?.CreatedUtc
                ?? assistantLines.FirstOrDefault()?.CreatedUtc
                ?? _clock.UtcNow;

            await ObserveRecordingDisclosureAsync(voiceEvent, disclosure, line, said, occurredUtc);
        }
        catch (Exception ex)
        {
            // The check is a record of the call; it must never fail the work that finishes the call.
            _logger.LogWarning(
                ex,
                "Could not check the recording disclosure of AI voice activity '{ActivityId}'.",
                voiceEvent.ActivityId.SanitizeLogValue());
        }
    }

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
