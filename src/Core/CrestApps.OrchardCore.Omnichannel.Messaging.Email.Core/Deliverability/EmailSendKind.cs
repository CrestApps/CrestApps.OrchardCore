namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;

/// <summary>
/// What an email is, for the limits and pauses it is held to.
/// </summary>
public enum EmailSendKind
{
    /// <summary>
    /// A person's own email in a conversation, or the workspace's acknowledgement of one. It is counted against the
    /// address's limits but never held back by them.
    /// </summary>
    Reply = 0,

    /// <summary>
    /// An answer automation (an AI agent or a workflow) wrote in a conversation. It runs at machine speed, so it waits
    /// for room under the address's hourly and daily limits and for its pauses; it keeps a reply's threading and takes
    /// no turn behind held-back bulk mail.
    /// </summary>
    Automated = 1,

    /// <summary>
    /// Bulk mail: a broadcast, a campaign's opening email or a follow-up. It is held to every limit and pause.
    /// </summary>
    Bulk = 2,
}
