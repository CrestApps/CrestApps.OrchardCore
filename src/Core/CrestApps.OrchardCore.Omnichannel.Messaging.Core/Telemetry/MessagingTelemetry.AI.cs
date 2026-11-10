using System.Diagnostics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Telemetry;

/// <summary>
/// The measurements of automated (AI) conversations: their turns, their decisions whether to reply, their handoffs
/// to a person, and the replies re-driven after they were lost.
/// </summary>
public static partial class MessagingTelemetry
{
    /// <summary>
    /// Starts measuring an automated (AI) conversation turn: the <c>crestapps.messaging.ai.turn</c> span,
    /// <see cref="AITurnDuration"/> and <see cref="AITurn"/>.
    /// </summary>
    /// <param name="channel">The technical name of the channel.</param>
    /// <returns>The measurement.</returns>
    public static MessagingMeasurement StartAITurn(string channel)
    {
        return Start("crestapps.messaging.ai.turn", AITurnDuration, AITurn, channel, ActivityKind.Internal);
    }

    /// <summary>
    /// Records a handoff of an automated conversation to a person.
    /// </summary>
    /// <param name="channel">The technical name of the channel.</param>
    /// <param name="result">The result, one of <see cref="HandoffResults"/> or <see cref="ResultError"/>.</param>
    public static void RecordAIHandoff(string channel, string result)
    {
        if (!AIHandoff.Enabled)
        {
            return;
        }

        Add(AIHandoff, new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ChannelTag, GetChannelTag(channel) },
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        });
    }

    /// <summary>
    /// Records whether an automated agent decided to reply.
    /// </summary>
    /// <param name="channel">The technical name of the channel.</param>
    /// <param name="decision">The decision, one of <see cref="RespondDecisions"/>.</param>
    public static void RecordRespondDecision(string channel, string decision)
    {
        if (!AIRespondDecision.Enabled)
        {
            return;
        }

        Add(AIRespondDecision, new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ChannelTag, GetChannelTag(channel) },
            { DecisionTag, decision },
        });
    }

    /// <summary>
    /// Records an automated conversation whose lost reply was re-driven.
    /// </summary>
    /// <param name="channel">The technical name of the channel.</param>
    /// <param name="result"><see cref="OwedReplyResults.Redriven"/> or <see cref="ResultError"/>.</param>
    public static void RecordOwedReplyRecovery(string channel, string result)
    {
        if (!OwedReplyRecovery.Enabled)
        {
            return;
        }

        Add(OwedReplyRecovery, new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ChannelTag, GetChannelTag(channel) },
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        });
    }

    /// <summary>
    /// The results of <see cref="AITurn"/>.
    /// </summary>
    public static class AITurnResults
    {
        /// <summary>
        /// The automated agent replied.
        /// </summary>
        public const string Replied = "replied";

        /// <summary>
        /// The automated agent judged that no reply was warranted.
        /// </summary>
        public const string NoReply = "no_reply";

        /// <summary>
        /// No customer message was owed a reply, because an earlier turn already answered it.
        /// </summary>
        public const string NotOwed = "not_owed";

        /// <summary>
        /// A newer customer message superseded the turn, which then composes the one reply.
        /// </summary>
        public const string Superseded = "superseded";

        /// <summary>
        /// The conversation lock was not acquired in time.
        /// </summary>
        public const string LockTimeout = "lock_timeout";

        /// <summary>
        /// The AI completion failed.
        /// </summary>
        public const string CompletionFailed = "completion_failed";

        /// <summary>
        /// The AI completion returned no content.
        /// </summary>
        public const string EmptyReply = "empty_reply";

        /// <summary>
        /// The provider refused the reply, or could not be reached.
        /// </summary>
        public const string SendFailed = "send_failed";
    }

    /// <summary>
    /// The results of <see cref="AIHandoff"/>.
    /// </summary>
    public static class HandoffResults
    {
        /// <summary>
        /// The conversation was handed to a person.
        /// </summary>
        public const string HandedOff = "handed_off";

        /// <summary>
        /// The handoff did not complete, so the automated agent keeps the conversation.
        /// </summary>
        public const string Failed = "failed";
    }

    /// <summary>
    /// The values of the <see cref="DecisionTag"/> of <see cref="AIRespondDecision"/>.
    /// </summary>
    public static class RespondDecisions
    {
        /// <summary>
        /// The agent decided to reply.
        /// </summary>
        public const string Respond = "respond";

        /// <summary>
        /// The agent decided a person would send nothing.
        /// </summary>
        public const string Skip = "skip";

        /// <summary>
        /// The decision could not be made, so the agent replies by default.
        /// </summary>
        public const string Fallback = "fallback";
    }

    /// <summary>
    /// The results of <see cref="OwedReplyRecovery"/>.
    /// </summary>
    public static class OwedReplyResults
    {
        /// <summary>
        /// The lost reply was re-driven through the automated agent.
        /// </summary>
        public const string Redriven = "redriven";
    }
}
