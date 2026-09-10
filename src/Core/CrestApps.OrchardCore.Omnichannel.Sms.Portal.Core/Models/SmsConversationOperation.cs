namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;

/// <summary>
/// The operations an agent can perform on an <see cref="SmsConversation"/>. Each one is authorized separately so
/// reading a thread, replying on it, taking ownership of it, and moving it between agents can carry different
/// rules.
/// </summary>
public enum SmsConversationOperation
{
    /// <summary>
    /// Read the thread and its messages.
    /// </summary>
    View,

    /// <summary>
    /// Send an outbound message on the thread.
    /// </summary>
    Send,

    /// <summary>
    /// Take ownership of a pooled or unassigned thread.
    /// </summary>
    Claim,

    /// <summary>
    /// Close the thread.
    /// </summary>
    Close,

    /// <summary>
    /// Snooze the thread until later.
    /// </summary>
    Snooze,

    /// <summary>
    /// Move the thread to another agent.
    /// </summary>
    Transfer,
}
