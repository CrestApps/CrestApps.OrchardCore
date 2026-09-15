namespace CrestApps.OrchardCore.ContactCenter;

public static partial class ContactCenterConstants
{
    /// <summary>
    /// Reason codes recorded when a queue limit, rather than an agent, decided how a call ended.
    /// </summary>
    public static class QueueLimits
    {
        /// <summary>
        /// The caller arrived at a full queue configured to send new arrivals to voicemail.
        /// </summary>
        public const string QueueFullVoicemailReasonCode = "queue_full_voicemail";

        /// <summary>
        /// The caller waited as long as the queue allows and the queue is configured to send them to voicemail.
        /// </summary>
        public const string MaxWaitVoicemailReasonCode = "queue_max_wait_voicemail";
    }
}
