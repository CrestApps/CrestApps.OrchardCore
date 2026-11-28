using CrestApps.Core.AI.Models;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Automation;

public sealed partial class AutomatedConversationHandler
{
    /// <summary>
    /// The longest reading pause a channel may ask for before a reply. Whatever the channel returns is capped here, so a
    /// turn always ends well within the conversation lock's expiry.
    /// </summary>
    internal static readonly TimeSpan MaxReadingDelay = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The longest typing pause a channel may ask for before a composed reply is sent.
    /// </summary>
    internal static readonly TimeSpan MaxTypingDelay = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How long the conversation lock lives before it lapses when its holder never releases it. A turn holds the lock
    /// through the reading pause, the should-respond judgement, the composition, the typing pause, the send and a handoff,
    /// so the expiry outlasts the longest pauses with minutes to spare for the model calls. A lock that lapsed mid-turn
    /// would let a deferred conclusion run meanwhile, and the turn would then overwrite its result. The conclusion and the
    /// follow-ups take the same lock with the same expiry.
    /// </summary>
    internal static readonly TimeSpan LockExpiration = MaxReadingDelay + MaxTypingDelay + TimeSpan.FromMinutes(9);

    /// <summary>
    /// The most messages the agent sends in one conversation within <see cref="ReplyLimitWindow"/>: replies, follow-ups
    /// and the opening message alike. An automated mailbox that sends no auto-reply headers would otherwise be answered
    /// forever; past the limit the agent stops replying and leaves the conversation to a person.
    /// </summary>
    internal const int MaxAgentMessagesPerWindow = 5;

    /// <summary>
    /// The window <see cref="MaxAgentMessagesPerWindow"/> counts over.
    /// </summary>
    internal static readonly TimeSpan ReplyLimitWindow = TimeSpan.FromHours(1);

    private const string NotWarrantedReason = "NotWarranted";
    private const string ReplyLimitReason = "ReplyLimit";

    private static TimeSpan Cap(TimeSpan delay, TimeSpan ceiling)
        => delay > ceiling ? ceiling : delay;

    // Reads the activity again from the database. YesSql hands back the instance this session already loaded, so the
    // stale copy is detached first; every later write must then use the fresh instance this returns.
    private async Task<OmnichannelActivity> ReloadActivityAsync(OmnichannelActivity loaded, CancellationToken cancellationToken)
    {
        if (loaded is null)
        {
            return null;
        }

        _session.Detach(loaded, OmnichannelConstants.CollectionName);

        return await _activityStore.FindByIdAsync(loaded.ItemId, cancellationToken);
    }

    // The conversation waits on the customer after a reply, or after a turn that warranted none. The activity is read
    // once more first: the send took time, and a conversation closed meanwhile keeps its terminal status.
    private async Task AwaitCustomerAsync(AutomatedConversationTurn turn, AutomatedTurnOutcome outcome, CancellationToken cancellationToken)
    {
        turn.Activity = await ReloadActivityAsync(turn.Activity, cancellationToken);

        var activity = turn.Activity;

        if (activity is null || activity.Status.IsTerminal())
        {
            return;
        }

        activity.Status = ActivityStatus.AwaitingCustomerAnswer;

        if (OmnichannelAutomationHelper.HasNoResponseTimeout(turn.FlowSettings))
        {
            activity.ScheduledUtc = OmnichannelAutomationHelper.ResolveNoResponseDeadline(turn.FlowSettings, _clock.UtcNow);
        }

        await _activityStore.UpdateAsync(activity, cancellationToken);

        ScheduleConclusion(turn, outcome.HangupRequested);
    }

    // Past the limit the agent sends nothing more and leaves the conversation to a person. The customer's message is
    // marked handled, so the owed-reply recovery does not raise it again every few minutes.
    private async Task<bool> TryApplyReplyLimitAsync(
        AutomatedConversationTurn turn,
        IReadOnlyList<AIChatSessionPrompt> conversation,
        List<AIChatSessionPrompt> pending,
        bool handoffAvailable,
        AutomatedTurnOutcome outcome,
        CancellationToken cancellationToken)
    {
        var sent = AutomatedConversationTranscript.CountAgentMessagesSince(conversation, _clock.UtcNow - ReplyLimitWindow);

        if (sent < MaxAgentMessagesPerWindow)
        {
            return false;
        }

        _logger.LogWarning("The automated agent already sent {Sent} {Channel} messages in Activity {ActivityId} within {WindowMinutes} minutes, so it does not reply and leaves the conversation to a person.", sent, turn.Channel.Channel, turn.Activity.ItemId.SanitizeLogValue(), ReplyLimitWindow.TotalMinutes);

        await MarkUnansweredAsync(turn, pending[^1], ReplyLimitReason, cancellationToken);

        LeaveToPerson(outcome, handoffAvailable, $"The automated assistant reached its limit of {MaxAgentMessagesPerWindow} messages within {ReplyLimitWindow.TotalMinutes} minutes.");

        return true;
    }

    // Records that the customer's latest message was handled without a reply, and commits it while the lock is held, so
    // the recovery and the follow-ups, which take the lock next, see the turn as handled. The follow-up delay counts from
    // this decision.
    private async Task MarkUnansweredAsync(AutomatedConversationTurn turn, AIChatSessionPrompt prompt, string reason, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        AutomatedConversationTranscript.MarkUnanswered(prompt, reason, now);

        await _promptStore.UpdateAsync(prompt, cancellationToken);

        turn.ChatSession.LastActivityUtc = now;
        await _chatSessionManager.SaveAsync(turn.ChatSession, cancellationToken);

        await _session.SaveChangesAsync(cancellationToken);
    }

    // Leaves the conversation to a person without a word to the customer: handed off when a person is available, so the
    // human thread takes the transcript; otherwise the turn is simply not answered.
    private static void LeaveToPerson(AutomatedTurnOutcome outcome, bool handoffAvailable, string reason)
    {
        if (!handoffAvailable)
        {
            return;
        }

        outcome.Handled = true;
        outcome.HandoffRequested = true;
        outcome.HandoffReason = reason;
    }
}
