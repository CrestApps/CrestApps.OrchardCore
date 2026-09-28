namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies where a queue line delivers a voicemail that no agent of its own is waiting for: a caller who chose
/// voicemail from the menu, reached it because the queue was full or they waited too long, or called while the line
/// was closed. A personal line's messages always go to its agent, and a message for an agent who let an offered call
/// ring out always goes to that agent, whichever destination is chosen here.
/// </summary>
public enum EntryPointVoicemailDestination
{
    /// <summary>
    /// The message goes to the soft-phone Voicemail tab of the entry point's voicemail inbox agent. This is the
    /// default, so an entry point saved before the destination could be chosen keeps delivering where it did.
    /// </summary>
    AgentInbox,

    /// <summary>
    /// The message goes to the shared voicemail box of the queue the caller was in, where every user entitled to that
    /// queue and allowed to access shared voicemail can review, claim and act on it.
    /// </summary>
    QueueSharedBox,
}
