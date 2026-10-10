namespace CrestApps.OrchardCore.Omnichannel.Automation;

/// <summary>
/// What an automated activity's opening message started, kept on the activity, so a follow-up continues the same thread:
/// on a channel whose messages carry a subject, a follow-up replies under the opening message's subject.
/// </summary>
public sealed class AutomatedConversationThread
{
    /// <summary>
    /// Gets or sets the subject the opening message was sent with, on a channel that has subjects.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the provider's identifier of the opening message, when the provider reported one.
    /// </summary>
    public string OpeningMessageId { get; set; }
}
