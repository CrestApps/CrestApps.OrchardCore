using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.Omnichannel.Voice.Models;
using Microsoft.Extensions.AI;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Stores the conversation's lines, each stamped with when it was said, so a recording of the call can be played
/// from any line of its transcript.
/// </summary>
public sealed partial class VoiceAgentConversationLoop
{
    private Task StorePromptAsync(AIChatSession session, ChatRole role, string content, CancellationToken cancellationToken)
        => StorePromptAsync(session, role, content, spokenUtc: null, cancellationToken);

    /// <summary>
    /// Stores a line of the conversation, stamped with when it was said.
    /// </summary>
    /// <param name="session">The conversation's session.</param>
    /// <param name="role">Who said it.</param>
    /// <param name="content">What was said.</param>
    /// <param name="spokenUtc">
    /// When the line began, when that is known better than now: a caller's line is only transcribed once they have
    /// finished it. The assistant's lines are stored just before they are spoken, so now is when they begin.
    /// </param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    private async Task StorePromptAsync(AIChatSession session, ChatRole role, string content, DateTime? spokenUtc, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        await _promptStore.CreateAsync(new AIChatSessionPrompt
        {
            ItemId = UniqueId.GenerateId(),
            SessionId = session.SessionId,
            Role = role,
            Content = content,

            // Unstamped, every turn of a turn-based call defaulted to DateTime.MinValue, so the transcript only kept
            // its order by accident and the call recordings page could not place a line on the recording. Never
            // later than now: a provider clock running ahead must not put a line after the reply to it.
            CreatedUtc = spokenUtc is { } spoken && spoken <= now ? spoken : now,
        }, cancellationToken);

        session.LastActivityUtc = now;
        await _chatSessionManager.SaveAsync(session, cancellationToken);
    }

    /// <summary>
    /// Estimates when the caller began the line just transcribed.
    /// </summary>
    /// <param name="voiceEvent">The transcription event.</param>
    /// <param name="caller">What the caller said.</param>
    /// <param name="prompts">The conversation so far, oldest first.</param>
    /// <returns>The estimated start, in UTC.</returns>
    /// <remarks>
    /// The transcription arrives once the caller has stopped talking, and the provider reports when; the line began
    /// about its own length before that (<see cref="SpokenLineTiming"/>). Never before the line it answers, which
    /// the caller could not talk over: the call stops listening while the assistant speaks.
    /// </remarks>
    private DateTime EstimateCallerLineStart(VoiceAgentEvent voiceEvent, string caller, IReadOnlyList<AIChatSessionPrompt> prompts)
    {
        var now = _clock.UtcNow;
        var heardUtc = voiceEvent.OccurredUtc is { } occurred && occurred <= now ? occurred : now;
        var startedUtc = heardUtc - SpokenLineTiming.EstimateDuration(caller);

        if (prompts.Count == 0)
        {
            return startedUtc;
        }

        // The caller spoke after the line before had been said, not while it was.
        var previous = prompts[^1];
        var previousEndedUtc = previous.CreatedUtc + SpokenLineTiming.EstimateDuration(previous.Content);

        if (startedUtc >= previousEndedUtc)
        {
            return startedUtc;
        }

        return previousEndedUtc < heardUtc ? previousEndedUtc : heardUtc;
    }
}
