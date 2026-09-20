using CrestApps.Core.Omnichannel.Services;
using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.Omnichannel.Voice.Services;
using CrestApps.Core.Omnichannel.Voice.Tools;
using CrestApps.Core.Omnichannel.Voice;
using CrestApps.Core;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.Core.Omnichannel.Voice.Models;
using CrestApps.Core.Telephony.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// What happens to a call the assistant is no longer the right answer for: the caller is handed to a person, or
/// the call is ended — and, for a live session, carrying that out somewhere the provider's abandoned webhook
/// request cannot take down with it.
/// </summary>
public sealed partial class VoiceAgentConversationLoop
{
    // Resolves the phone handoff service and this subject's flow settings, returning a non-null service only when
    // handoff is both configured (enabled with a target queue) and a channel implementation is registered. The flow
    // settings are always returned so callers can read the target queue.
    private async Task<(IOmnichannelHandoffService Service, SubjectFlowSettings FlowSettings)> ResolveVoiceHandoffAsync(
        OmnichannelActivity activity,
        CancellationToken cancellationToken)
    {
        var flowSettings = string.IsNullOrWhiteSpace(activity.SubjectContentType)
            ? null
            : await _subjectFlowSettingsService.FindConfiguredFlowSettingsAsync(activity.SubjectContentType, cancellationToken);

        if (!OmnichannelHandoffHelper.IsHandoffEnabled(flowSettings))
        {
            return (null, flowSettings);
        }

        var service = _handoffServices?.FirstOrDefault(candidate => candidate.CanHandle(OmnichannelConstants.Channels.Phone));

        return (service, flowSettings);
    }

    // Seats the still-connected caller in the configured queue and offers the call to an agent, reusing the inbound
    // enqueue-and-offer pipeline. On success the call stays up while the queue rings an agent; on failure there is
    // nowhere to route the caller, so the call is ended.
    /// <summary>
    /// Clears the durable "a transfer is pending" flag, so the speak.ended raised by whatever we say next does
    /// not come back around and perform the transfer again.
    /// </summary>
    /// <param name="activity">The activity being handed off.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private async Task ClearPendingHandoffAsync(OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        // Re-read: the handoff moved this row through another service, so the copy held here is already stale.
        var current = await _activityStore.FindByIdAsync(activity.ItemId, cancellationToken);

        if (current is not null && current.Properties.Remove(nameof(PendingVoiceHandoff)))
        {
            await _activityStore.UpdateAsync(current, cancellationToken);
        }

        // Also on the in-memory copy the caller is still holding, so a second pass inside this same scope sees
        // the flag gone rather than re-reading it from a row it has not reloaded.
        activity.Properties?.Remove(nameof(PendingVoiceHandoff));
    }

    /// <summary>
    /// Hands whatever the finished live session decided to a scope of its own, so it is carried out somewhere
    /// the request that held the session cannot take down with it.
    /// </summary>
    /// <remarks>
    /// A realtime session holds the caller for the whole call, inside the provider's "call answered" webhook
    /// request. No provider waits minutes for a webhook response: ours timed out, was retried, and had its
    /// connection aborted the moment the session ended — and the work that came after, the handoff, went with it.
    /// Observed live: the model asked to transfer, the tool recorded it, the caller heard the assistant stop, and
    /// nothing ever enqueued them.
    /// <para>
    /// Nothing thrown here escapes. This is called while the session may itself be failing, and an exception
    /// raised on the way out would replace the one that explains why.
    /// </para>
    /// </remarks>
    /// <param name="activity">The call the session was held for.</param>
    /// <param name="voiceEvent">The provider event the session was started from.</param>
    private async Task FinishTheCallElsewhereAsync(OmnichannelActivity activity, VoiceAgentEvent voiceEvent)
    {
        // Read here, while the turns that recorded them are still this scope's. Everything after this point runs
        // somewhere else and can carry nothing but plain values.
        var handoffRequested = _handoffTurn.HandoffRequested;
        var endCallRequested = _endCallTurn.EndCallRequested;

        // The caller hung up, or the session never got far enough to decide anything. There is nothing to finish.
        if (!handoffRequested && !endCallRequested)
        {
            return;
        }

        try
        {
            await _completionRunner.RunAsync(new RealtimeCallCompletion
            {
                ActivityId = activity.ItemId,
                ProviderName = voiceEvent.ProviderName,
                ProviderCallId = voiceEvent.ProviderCallId,
                HandoffRequested = handoffRequested,
                EndCallRequested = endCallRequested,
                EndCallReason = _endCallTurn.Reason,
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "The automated call for activity '{ActivityId}' could not be handed on to be finished; the caller is still on the line.",
                activity.ItemId.SanitizeLogValue());
        }
    }

    /// <summary>
    /// Carries out what a finished live session decided: hand the caller to an agent, or end the call.
    /// </summary>
    /// <remarks>
    /// Runs in a scope of its own, on a fresh instance of this loop, because the scope the session ran in belongs
    /// to a webhook request the provider abandoned minutes ago. Everything is re-read here rather than carried
    /// across: the activity may have moved on while the call was up.
    /// </remarks>
    /// <param name="completion">What the session decided, and the call it decided it about.</param>
    internal async Task FinishRealtimeCallAsync(RealtimeCallCompletion completion)
    {
        ArgumentNullException.ThrowIfNull(completion);

        // Its own token, not the request's. The request's was cancelled when the provider gave up waiting, and
        // passing it here is what silently dropped the handoff.
        var cancellationToken = CancellationToken.None;

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Finishing the automated call for activity '{ActivityId}' on its own scope.",
                completion.ActivityId.SanitizeLogValue());
        }

        var media = _mediaResolver.Get(completion.ProviderName) ?? _mediaResolver.GetDefault();

        if (media is null)
        {
            _logger.LogWarning(
                "No automated voice media is registered for provider '{Provider}'; the call for activity '{ActivityId}' cannot be finished.",
                completion.ProviderName.SanitizeLogValue(),
                completion.ActivityId.SanitizeLogValue());

            return;
        }

        var activity = await _activityStore.FindByIdAsync(completion.ActivityId, cancellationToken);

        if (activity is null)
        {
            return;
        }

        var voiceEvent = new VoiceAgentEvent
        {
            ActivityId = completion.ActivityId,
            ProviderName = completion.ProviderName,
            ProviderCallId = completion.ProviderCallId,
        };

        // The caller asked for a person: seat them in the queue, on the same enqueue-and-offer path the
        // turn-based loop uses, so a handoff means the same thing on both.
        if (completion.HandoffRequested)
        {
            await PerformVoiceHandoffAsync(voiceEvent, media, activity, cancellationToken);

            return;
        }

        // The conversation finished and the call is still up, so somebody has to end it. The session already
        // waited for the closing line and gave the caller their moment; all that is left is the hangup.
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Ending automated call for activity '{ActivityId}': {Reason}",
                completion.ActivityId.SanitizeLogValue(),
                (completion.EndCallReason ?? "the model reported the conversation finished").SanitizeLogValue());
        }

        await media.HangupAsync(completion.ProviderCallId, cancellationToken);
    }

    private async Task PerformVoiceHandoffAsync(VoiceAgentEvent voiceEvent, IVoiceAgentMediaProvider media, OmnichannelActivity activity, CancellationToken cancellationToken)
    {
        var (handoffService, flowSettings) = await ResolveVoiceHandoffAsync(activity, cancellationToken);

        if (handoffService is null)
        {
            _logger.LogWarning("An AI voice handoff was requested for Activity {ActivityId} but no handoff destination is available; ending the call.", activity.ItemId.SanitizeLogValue());
            await media.HangupAsync(voiceEvent.ProviderCallId, cancellationToken);

            return;
        }

        OmnichannelHandoffResult result;

        try
        {
            result = await handoffService.RequestHandoffAsync(new OmnichannelHandoffRequest
            {
                Activity = activity,
                TargetQueueId = flowSettings.HandoffQueueId,
                Reason = "The automated assistant escalated the call to a live agent.",
                ContactAddress = activity.PreferredDestination,
                ProviderName = voiceEvent.ProviderName,
                ProviderCallId = voiceEvent.ProviderCallId,
                // Carried onto the interaction so the answering agent sees what the caller has already been
                // through, and can open the transcript rather than asking them to repeat it.
                AiSessionId = activity.AISessionId,
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The AI voice handoff for Activity {ActivityId} threw; ending the call.", activity.ItemId.SanitizeLogValue());
            await media.HangupAsync(voiceEvent.ProviderCallId, cancellationToken);

            return;
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning("The AI voice handoff for Activity {ActivityId} did not complete: {Reason}. Ending the call.", activity.ItemId.SanitizeLogValue(), result.Message);
            await media.HangupAsync(voiceEvent.ProviderCallId, cancellationToken);

            return;
        }

        // The handoff has now happened, so clear the durable flag before anything else is spoken.
        //
        // Every branch below says something to the caller, and speaking raises another speak.ended, which comes
        // straight back into the handler that re-reads this flag. Leaving it set turned one transfer into an
        // endless loop: a caller heard "Thanks for waiting..." seven times in forty-five seconds, once every few
        // seconds until they hung up. It used to be cleared only on the after-hours branch, which is why the
        // ordinary routed transfer — much the commoner path — was the one that repeated.
        await ClearPendingHandoffAsync(activity, cancellationToken);

        // After hours the destination queue is closed, so a callback was scheduled instead of routing the live
        // call. Tell the caller and end the call gracefully. The closing line is spoken and stored with the
        // hangup marker so the next speak.ended reaches the hangup path.
        if (result.Disposition == HandoffDisposition.CallbackScheduled)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("AI voice Activity {ActivityId} could not route live (after hours); a callback was scheduled.", activity.ItemId.SanitizeLogValue());
            }

            const string closing = "Thanks for your patience. Our specialists aren't available right now, so we've scheduled a callback and someone will reach out to you shortly. Goodbye.";

            if (!string.IsNullOrWhiteSpace(activity.AISessionId))
            {
                var session = await _chatSessionManager.FindByIdAsync(activity.AISessionId, cancellationToken);

                if (session is not null)
                {
                    // Store with the hangup marker so the next speak.ended ends the call.
                    await StorePromptAsync(session, ChatRole.Assistant, closing + " " + HangupMarker, cancellationToken);
                }
            }

            await SpeakAsync(media, voiceEvent.ProviderCallId, activity, closing, cancellationToken);

            return;
        }

        // "Connecting you now" and "you are in a queue" are different promises. Saying the first to a caller
        // nobody is free to take leaves them listening to silence, waiting for a person who was never offered
        // the call, so each disposition gets its own line.
        var routed = result.Disposition == HandoffDisposition.Routed;

        var handoffLine = routed
            ? "Thanks for waiting. I'm connecting you to a specialist now."
            : "Thanks for waiting. All of our specialists are busy right now, so I've placed you in the queue and the next available person will be with you shortly.";

        await SpeakAsync(media, voiceEvent.ProviderCallId, activity, handoffLine, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Handed off AI voice Activity {ActivityId} to a live agent; {OfferState}.",
                activity.ItemId.SanitizeLogValue(),
                routed ? "the call was offered to an available agent" : "the call is held while the queue waits for one");
        }
    }
}
