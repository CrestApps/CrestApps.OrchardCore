using System.Text;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Realtime;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Keeping a call alive when the session carrying it is not: a session lost with the caller still on the line is
/// replaced with a new one that knows what has been said, and one that cannot be replaced is reported rather than
/// left holding a silent line.
/// </summary>
/// <remarks>
/// Before this, whichever of the two pumps ended first ended the call, so a session that closed took the call down
/// with it while the caller was still there. Live, one provider error did exactly that: the media stream was stopped,
/// nothing was decided, and the caller sat in fifty seconds of silence before hanging up.
/// </remarks>
public sealed partial class RealtimeVoiceConversationRunner
{
    /// <summary>
    /// How many times one call's session is replaced before the call is given up as lost.
    /// </summary>
    /// <remarks>
    /// A session that fails once is usually a blip; one that keeps failing is a provider that is down, and every
    /// attempt is a few seconds of the caller hearing nothing. Two keeps that short.
    /// </remarks>
    internal const int MaximumSessionRecoveries = 2;

    /// <summary>
    /// How much of the conversation so far a replacement session is given, in characters, keeping the most recent.
    /// </summary>
    private const int MaximumResumeTranscriptLength = 6_000;

    /// <summary>
    /// How long a session that just ended is given for the caller's side to end too, before it is replaced.
    /// </summary>
    /// <remarks>
    /// A provider closing its session and the caller hanging up are often the same moment seen from two sockets.
    /// Replacing the session on a call that has already gone would open, and pay for, a session nobody hears.
    /// </remarks>
    private static readonly TimeSpan SessionLossSettle = TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// What a replacement session is asked to say first.
    /// </summary>
    private const string ResumeInstructions =
        "The line cut out for a moment and has just come back. Say one short sentence only: apologize briefly for " +
        "the interruption and pick up exactly where the conversation left off. Do not greet the customer or " +
        "introduce yourself again.";

    /// <summary>
    /// Runs the model's side of the call for as long as the caller is on it, replacing the session when it is lost.
    /// </summary>
    /// <param name="media">The call's media session.</param>
    /// <param name="live">The session currently carrying the call.</param>
    /// <param name="context">The call being held.</param>
    /// <param name="ambience">The room bed, when the call has one.</param>
    /// <param name="bargeIn">What is queued on the line, shared with the caller's pump.</param>
    /// <param name="toModel">The caller's pump, which ends when the caller does.</param>
    /// <param name="callScope">The scope whose cancellation ends the call.</param>
    private async Task HoldTheConversationAsync(
        IContactCenterVoiceMediaSession media,
        LiveConversation live,
        RealtimeVoiceConversationContext context,
        CallAmbience ambience,
        AssistantBargeIn bargeIn,
        Task toModel,
        CancellationTokenSource callScope)
    {
        var recoveries = 0;

        while (true)
        {
            var toCaller = PumpAssistantAudioAsync(media, live.Current, context, ambience, bargeIn, live.SessionToken);

            if (await Task.WhenAny(toModel, toCaller) == toModel)
            {
                // The caller has gone. What the model was doing no longer matters to anybody.
                await callScope.CancelAsync();
                await Settle(toCaller);

                return;
            }

            var lost = await toCaller || live.Faulted;

            // The call is ending on purpose -- a goodbye, a transfer, the caller hanging up -- or the session ended
            // because it was asked to. None of those is a session being lost.
            if (!lost ||
                callScope.IsCancellationRequested ||
                context.EndCallRequested.IsCancellationRequested ||
                context.HandoffRequested.IsCancellationRequested)
            {
                return;
            }

            await Task.WhenAny(toModel, Task.Delay(SessionLossSettle, callScope.Token));

            if (toModel.IsCompleted || callScope.IsCancellationRequested)
            {
                return;
            }

            if (recoveries >= MaximumSessionRecoveries || !await ReconnectAsync(live, context, ++recoveries, callScope.Token))
            {
                // Said loudly, because what happens next -- a person, or an apology and a hangup -- is decided by
                // whoever held the call, and without this the only trace is a call that went quiet.
                context.SessionLost = true;
                _meter?.Failed();
                _logger.LogError(
                    "The realtime session on activity '{ActivityId}' was lost with the caller still on the line and could not be brought back after {Attempts} attempt(s).",
                    context.Activity?.ItemId.SanitizeLogValue(),
                    recoveries);

                return;
            }
        }
    }

    /// <summary>
    /// Replaces a lost session with a new one that has been told what was said, and asks it to pick the call up.
    /// </summary>
    /// <returns><see langword="true"/> when the call is being carried by a new session.</returns>
    private async Task<bool> ReconnectAsync(
        LiveConversation live,
        RealtimeVoiceConversationContext context,
        int attempt,
        CancellationToken callToken)
    {
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning(
                "The realtime session on activity '{ActivityId}' was lost with the caller still on the line; opening a new one (attempt {Attempt} of {Maximum}).",
                context.Activity?.ItemId.SanitizeLogValue(),
                attempt,
                MaximumSessionRecoveries);
        }

        // Let go of the old one first, so nothing keeps talking to a session that is gone while the next one opens.
        await live.DetachAsync();

        IRealtimeConversation next;

        try
        {
            next = await StartConversationAsync(context, await DescribeConversationSoFarAsync(context, callToken), callToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        if (next is null)
        {
            return false;
        }

        try
        {
            await ApplyTelephonyTurnDetectionAsync(next, callToken);
            await next.RequestUnpromptedResponseAsync(ResumeInstructions, callToken);
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "A replacement realtime session on activity '{ActivityId}' opened but could not be started.", context.Activity?.ItemId.SanitizeLogValue());
            }

            await next.DisposeAsync();

            return false;
        }

        live.Attach(next, callToken);

        return true;
    }

    /// <summary>
    /// The conversation so far, written as the instructions a replacement session is given.
    /// </summary>
    /// <remarks>
    /// A new session starts with no history at all. Without this it would do what the first did — open the call —
    /// and the caller who has been talking for two minutes would be greeted as though they had just picked up.
    /// </remarks>
    private async Task<string> DescribeConversationSoFarAsync(RealtimeVoiceConversationContext context, CancellationToken cancellationToken)
    {
        IReadOnlyList<AIChatSessionPrompt> prompts;

        try
        {
            prompts = (await _promptStore.GetPromptsAsync(context.Session.SessionId))?.ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not read the conversation so far on activity '{ActivityId}' for its replacement session.", context.Activity?.ItemId.SanitizeLogValue());
            prompts = null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var lines = new List<string>();
        var length = 0;

        foreach (var prompt in (prompts ?? []).Where(prompt => !prompt.IsGeneratedPrompt && !string.IsNullOrWhiteSpace(prompt.Content)).Reverse())
        {
            var line = $"{(prompt.Role == ChatRole.Assistant ? "You" : "Customer")}: {prompt.Content.Trim()}";

            if (length + line.Length > MaximumResumeTranscriptLength)
            {
                break;
            }

            lines.Insert(0, line);
            length += line.Length;
        }

        var builder = new StringBuilder()
            .AppendLine("## The call so far")
            .AppendLine()
            .AppendLine(
                "You are already partway through this call. The line dropped for a moment and has just been " +
                "restored. Carry on from where the conversation below left off; do not greet the customer or " +
                "introduce yourself again.");

        if (lines.Count > 0)
        {
            builder.AppendLine();

            foreach (var line in lines)
            {
                builder.AppendLine(line);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// The session currently carrying a call, which can be replaced while the call goes on.
    /// </summary>
    /// <remarks>
    /// The caller's pump and the idle watchdog read <see cref="Current"/> on every use rather than holding the
    /// session they started with. While a replacement is opening there is none, and the caller's audio for that
    /// moment is simply not sent: there is nowhere for it to go.
    /// </remarks>
    private sealed class LiveConversation : IAsyncDisposable
    {
        private IRealtimeConversation _current;
        private CancellationTokenSource _session;
        private volatile bool _faulted;

        public LiveConversation(IRealtimeConversation conversation, CancellationToken callToken)
            => Attach(conversation, callToken);

        /// <summary>
        /// The session carrying the call, or <see langword="null"/> while one is being replaced.
        /// </summary>
        public IRealtimeConversation Current => Volatile.Read(ref _current);

        /// <summary>
        /// Cancelled when the call ends, or when the current session is found to be dead.
        /// </summary>
        public CancellationToken SessionToken => _session.Token;

        /// <summary>
        /// Whether the current session was found to be dead by something other than its own event stream.
        /// </summary>
        public bool Faulted => _faulted;

        /// <summary>
        /// Marks a session dead — sending to it failed — so the pump reading it stops and the call can replace it.
        /// </summary>
        /// <param name="conversation">The session that failed, so a late report about an old one is ignored.</param>
        public void Fault(IRealtimeConversation conversation)
        {
            if (!ReferenceEquals(conversation, Current))
            {
                return;
            }

            _faulted = true;

            try
            {
                _session.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The call is already over.
            }
        }

        public void Attach(IRealtimeConversation conversation, CancellationToken callToken)
        {
            var previous = _session;

            _session = CancellationTokenSource.CreateLinkedTokenSource(callToken);
            _faulted = false;
            previous?.Dispose();

            Volatile.Write(ref _current, conversation);
        }

        public async Task DetachAsync()
        {
            var previous = Interlocked.Exchange(ref _current, null);

            if (previous is null)
            {
                return;
            }

            try
            {
                await previous.DisposeAsync();
            }
            catch (Exception)
            {
                // It is already gone; that is why it is being let go of.
            }
        }

        public async ValueTask DisposeAsync()
        {
            await DetachAsync();
            _session?.Dispose();
        }
    }
}
