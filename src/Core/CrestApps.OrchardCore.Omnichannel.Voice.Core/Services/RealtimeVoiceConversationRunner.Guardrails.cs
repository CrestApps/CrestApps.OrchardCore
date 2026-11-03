using CrestApps.Core.AI.Guardrails;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Realtime;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// The profile's guardrails on a live call: what the caller says is checked as it is transcribed, and a refused
/// request is answered with the profile's placeholder reply instead of the model's.
/// </summary>
/// <remarks>
/// <para>
/// A live session answers the caller as soon as they stop talking, before their words are transcribed, so a check
/// cannot stand in front of the model the way it does on a turn-based call. It runs beside the call instead, and
/// adds nothing to an ordinary turn. When it refuses, the model is told not to act on the request. A reply still
/// being spoken is taken off the line and out of the transcript, and the model is asked, in the conversation, to
/// give the placeholder instead. A reply already finished is left: live, the check came back after the model had
/// already declined in its own words.
/// </para>
/// <para>
/// The placeholder is asked for inside the conversation, like a supervisor's note. Asked for as a standalone message
/// under the session's instructions, the model had no conversation to go on and gave its opening line again: live,
/// twice in one call.
/// </para>
/// <para>
/// The check runs in a scope of its own: a refusal starts the AI Guardrail Triggered workflows, and those cannot
/// share the call's database session with the loop that is carrying its audio.
/// </para>
/// </remarks>
public sealed partial class RealtimeVoiceConversationRunner
{
    /// <summary>
    /// What the model is told when the caller's last request was refused.
    /// </summary>
    internal const string RefusedRequestNote =
        "[The customer's last request is about a topic this business does not discuss. Do not answer it, act on it " +
        "or call a tool for it, now or later in the call.]";

    // Set while a refused request's reply is kept off the line, and cleared when the placeholder starts.
    private int _guardrailMuted;

    // Set once the placeholder has been asked for, so the response that starts next is the one let through.
    private int _guardrailReplyRequested;

    // How many of the assistant's lines have been heard, so a check can tell whether the refused request was already
    // answered.
    private int _assistantLinesHeard;

    /// <summary>
    /// Gets or sets how a caller's line is checked: the profile, the line and the session in, the placeholder reply
    /// out when the line is refused. Runs the profile's guardrails in a scope of their own unless replaced.
    /// </summary>
    internal Func<AIProfile, string, string, CancellationToken, Task<string>> CheckCallerLine { get; set; }

    /// <summary>
    /// Builds the note that asks the model to give the placeholder, when its own reply was taken off the line.
    /// </summary>
    /// <param name="placeholder">The profile's placeholder reply.</param>
    /// <returns>The note.</returns>
    internal static string RefusalReplyNote(string placeholder)
        => RefusedRequestNote + " Your reply to it was not heard. Tell the customer this now, in the language of the " +
            "call, and nothing else: \"" + placeholder?.Trim() + "\"";

    /// <summary>
    /// Counts an assistant line the caller heard.
    /// </summary>
    private void AssistantLineHeard()
        => Interlocked.Increment(ref _assistantLinesHeard);

    /// <summary>
    /// Whether the guardrails let what the model is saying reach the line.
    /// </summary>
    private bool TheGuardrailsAllowTheLine()
        => Volatile.Read(ref _guardrailMuted) == 0;

    /// <summary>
    /// Whether the response that is starting is the placeholder, which is let through to the line again.
    /// </summary>
    private bool ThePlaceholderIsStarting()
    {
        if (Interlocked.CompareExchange(ref _guardrailReplyRequested, 0, 1) != 1)
        {
            return false;
        }

        Volatile.Write(ref _guardrailMuted, 0);

        return true;
    }

    /// <summary>
    /// Checks a caller's line against the profile's guardrails beside the call, without holding up its audio.
    /// </summary>
    /// <param name="media">The call's media session.</param>
    /// <param name="conversation">The session carrying the call.</param>
    /// <param name="context">The call being held.</param>
    /// <param name="line">What the caller said.</param>
    /// <param name="callToken">The token cancelled when the call ends.</param>
    private void GuardCallerLine(
        IContactCenterVoiceMediaSession media,
        IRealtimeConversation conversation,
        RealtimeVoiceConversationContext context,
        string line,
        CancellationToken callToken)
    {
        var check = CheckCallerLine ?? CheckInOwnScopeAsync;

        if (string.IsNullOrWhiteSpace(line) ||
            (CheckCallerLine is null && _shellHost is null) ||
            !AIGuardrailService.IsOn(context.Profile, AIGuardrailDirection.Customer))
        {
            return;
        }

        var heardBefore = Volatile.Read(ref _assistantLinesHeard);

        _ = Task.Run(() => RefuseIfGuardedAsync(media, conversation, context, line, check, heardBefore, callToken), CancellationToken.None);
    }

    private async Task RefuseIfGuardedAsync(
        IContactCenterVoiceMediaSession media,
        IRealtimeConversation conversation,
        RealtimeVoiceConversationContext context,
        string line,
        Func<AIProfile, string, string, CancellationToken, Task<string>> check,
        int heardBefore,
        CancellationToken callToken)
    {
        try
        {
            var placeholder = await check(context.Profile, line, context.Session?.SessionId, callToken);

            if (string.IsNullOrWhiteSpace(placeholder))
            {
                return;
            }

            // The model has already answered and nothing of it is still playing: what it said stands, and it is only
            // told not to act on the request from here on.
            if (Volatile.Read(ref _responseInFlight) == 0 && Volatile.Read(ref _assistantLinesHeard) > heardBefore)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation(
                        "A guardrail refused the caller's request on activity '{ActivityId}' after the model had answered it; the model is told not to act on it.",
                        context.Activity?.ItemId.SanitizeLogValue());
                }

                await conversation.AddMessageAsync(ChatRole.System, RefusedRequestNote, callToken);

                return;
            }

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "A guardrail refused the caller's request on activity '{ActivityId}'; the model's reply is kept off the line and the placeholder is said instead.",
                    context.Activity?.ItemId.SanitizeLogValue());
            }

            // Whatever the model has started saying to the refused request stops reaching the caller now.
            Volatile.Write(ref _guardrailMuted, 1);

            var now = DateTime.UtcNow.Ticks;
            Interlocked.Exchange(ref _assistantSpeechEndsTicks, now);
            Interlocked.Exchange(ref _assistantVoiceEndsTicks, Math.Min(Interlocked.Read(ref _assistantVoiceEndsTicks), now));

            await media.ClearOutgoingAsync(callToken);

            // The refused reply finishes, unheard, before the placeholder is asked for: two responses at once would
            // talk over each other.
            var waitedUntil = DateTime.UtcNow + SupervisorNoteWait;

            while (Volatile.Read(ref _responseInFlight) == 1 && DateTime.UtcNow < waitedUntil)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), callToken);
            }

            await conversation.AddMessageAsync(ChatRole.System, RefusalReplyNote(placeholder), callToken);

            Volatile.Write(ref _guardrailReplyRequested, 1);

            await conversation.RequestUnpromptedResponseAsync(cancellationToken: callToken);
        }
        catch (OperationCanceledException)
        {
            // The call ended while its line was being checked.
        }
        catch (Exception ex)
        {
            // A check that fails lets the call go on as it was.
            Volatile.Write(ref _guardrailMuted, 0);
            Volatile.Write(ref _guardrailReplyRequested, 0);

            _logger.LogWarning(ex, "Could not check the caller's line on activity '{ActivityId}' against the profile's guardrails.", context.Activity?.ItemId.SanitizeLogValue());
        }
    }

    private async Task<string> CheckInOwnScopeAsync(AIProfile profile, string line, string sessionId, CancellationToken cancellationToken)
    {
        var scope = await _shellHost.GetScopeAsync(_shellSettings);
        string placeholder = null;

        await scope.UsingAsync(async childScope =>
        {
            var verdict = await childScope.ServiceProvider
                .GetRequiredService<AIGuardrailService>()
                .CheckAsync(profile, line, AIGuardrailDirection.Customer, sessionId, cancellationToken);

            placeholder = verdict?.Reply;
        });

        return placeholder;
    }
}
