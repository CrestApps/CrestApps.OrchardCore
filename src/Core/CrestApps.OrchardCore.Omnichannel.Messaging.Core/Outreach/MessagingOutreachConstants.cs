namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Outreach;

/// <summary>
/// The names Messaging registers with the outreach engine.
/// </summary>
public static class MessagingOutreachConstants
{
    /// <summary>
    /// The owner type of the runs that send broadcasts.
    /// </summary>
    public const string BroadcastOwnerType = "MessagingBroadcast";

    /// <summary>
    /// The audience source that pages a broadcast's recipient list.
    /// </summary>
    public const string BroadcastAudienceSource = "MessagingBroadcastRecipients";

    /// <summary>
    /// The channel sender that sends on the messaging channels, each recipient on their own, opening no conversation
    /// until the recipient replies.
    /// </summary>
    public const string Sender = "Messaging";
}
