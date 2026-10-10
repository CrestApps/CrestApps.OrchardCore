namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies who gives the recording disclosure on a call, which decides whether the tenant gives it at all.
/// </summary>
public enum RecordingDisclosureCallType
{
    /// <summary>
    /// The platform speaks the disclosure to an inbound caller on an entry point, before anything else they hear.
    /// </summary>
    Inbound,

    /// <summary>
    /// An automated voice agent gives the disclosure before anything else it says.
    /// </summary>
    AIVoiceAgent,

    /// <summary>
    /// The agent on the call reads the disclosure out and confirms it, because the caller has not heard it.
    /// </summary>
    Agent,
}
