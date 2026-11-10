using System.Diagnostics;
using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Telemetry;

/// <summary>
/// The measurements of outreach: broadcasts and their recipients, the automated conversations the activity starters
/// decide on, and the re-engagement nudges for automated conversations that went quiet.
/// </summary>
public static partial class MessagingTelemetry
{
    /// <summary>
    /// Records a broadcast that started or finished.
    /// </summary>
    /// <param name="channel">The technical name of the channel.</param>
    /// <param name="result">The result, one of <see cref="BroadcastResults"/>.</param>
    public static void RecordBroadcast(string channel, string result)
    {
        if (!Broadcast.Enabled)
        {
            return;
        }

        Add(Broadcast, new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ChannelTag, GetChannelTag(channel) },
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        });
    }

    /// <summary>
    /// Records one broadcast recipient sent to.
    /// </summary>
    /// <param name="channel">The technical name of the channel.</param>
    /// <param name="succeeded">Whether the message left.</param>
    public static void RecordBroadcastRecipient(string channel, bool succeeded)
    {
        if (!BroadcastRecipient.Enabled)
        {
            return;
        }

        Add(BroadcastRecipient, new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ChannelTag, GetChannelTag(channel) },
            { CrestAppsOrchardCoreTelemetry.ResultTag, succeeded ? BroadcastResults.Sent : BroadcastResults.Failed },
        });
    }

    /// <summary>
    /// Records what an activity starter decided about an automated conversation.
    /// </summary>
    /// <param name="channel">The technical name of the channel.</param>
    /// <param name="trigger"><see cref="ActivityStartResults.TriggerEntryPoint"/> or <see cref="ActivityStartResults.TriggerAutomated"/>.</param>
    /// <param name="result">The result, one of <see cref="ActivityStartResults"/>.</param>
    public static void RecordActivityStart(string channel, string trigger, string result)
    {
        if (!ActivityStart.Enabled)
        {
            return;
        }

        Add(ActivityStart, new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ChannelTag, GetChannelTag(channel) },
            { CrestAppsOrchardCoreTelemetry.TriggerTag, trigger },
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        });
    }

    /// <summary>
    /// The results of a re-engagement nudge, counted as its background task's items.
    /// </summary>
    public static class ReEngagementResults
    {
        /// <summary>
        /// The nudge was sent.
        /// </summary>
        public const string Sent = "sent";

        /// <summary>
        /// The provider refused the nudge, or could not be reached.
        /// </summary>
        public const string SendFailed = "send_failed";

        /// <summary>
        /// The step produced no text to send.
        /// </summary>
        public const string NoContent = "no_content";

        /// <summary>
        /// The contact opted out, so no nudge is sent.
        /// </summary>
        public const string OptedOut = "opted_out";

        /// <summary>
        /// It is outside the campaign's business hours, or its calendar cannot be evaluated.
        /// </summary>
        public const string OutsideHours = "outside_hours";

        /// <summary>
        /// A live reply holds the conversation, so the nudge waits for the next pass.
        /// </summary>
        public const string Busy = "busy";
    }

    /// <summary>
    /// The results of <see cref="Broadcast"/> and <see cref="BroadcastRecipient"/>.
    /// </summary>
    public static class BroadcastResults
    {
        /// <summary>
        /// The broadcast was handed to the outreach engine.
        /// </summary>
        public const string Started = "started";

        /// <summary>
        /// The broadcast lacked a channel, a sending address or a body, and was failed without sending.
        /// </summary>
        public const string Rejected = "rejected";

        /// <summary>
        /// The broadcast finished sending.
        /// </summary>
        public const string Completed = "completed";

        /// <summary>
        /// The broadcast run failed or was canceled.
        /// </summary>
        public const string Failed = "failed";

        /// <summary>
        /// The message to a recipient left.
        /// </summary>
        public const string Sent = "sent";
    }

    /// <summary>
    /// The triggers and results of <see cref="ActivityStart"/>.
    /// </summary>
    public static class ActivityStartResults
    {
        /// <summary>
        /// The trigger of a conversation an inbound text starts on a number whose entry point routes to an AI agent.
        /// </summary>
        public const string TriggerEntryPoint = "entry_point";

        /// <summary>
        /// The trigger of an automated activity the activity processor starts, such as a campaign's first text.
        /// </summary>
        public const string TriggerAutomated = "automated";

        /// <summary>
        /// The conversation was started.
        /// </summary>
        public const string Started = "started";

        /// <summary>
        /// An automated conversation was already running for the contact.
        /// </summary>
        public const string AlreadyActive = "already_active";

        /// <summary>
        /// The contact's last automated conversation ended too recently to start another.
        /// </summary>
        public const string QuietPeriod = "quiet_period";

        /// <summary>
        /// A person is already talking with the contact on the number.
        /// </summary>
        public const string HumanConversation = "human_conversation";

        /// <summary>
        /// The AI profile is missing or is not a chat profile.
        /// </summary>
        public const string ProfileMissing = "profile_missing";

        /// <summary>
        /// A starting workflow declined the conversation.
        /// </summary>
        public const string Declined = "declined";

        /// <summary>
        /// The contact opted out, so nothing was sent.
        /// </summary>
        public const string OptedOut = "opted_out";

        /// <summary>
        /// The provider refused the opening message, or could not be reached.
        /// </summary>
        public const string SendFailed = "send_failed";
    }
}
