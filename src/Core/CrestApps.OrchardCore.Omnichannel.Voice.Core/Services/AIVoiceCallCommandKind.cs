namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// What a supervisor can ask of a live AI call.
/// </summary>
public enum AIVoiceCallCommandKind
{
    /// <summary>
    /// Give the AI a note to act on.
    /// </summary>
    Instruct,

    /// <summary>
    /// Stop the AI and hand the caller to a person.
    /// </summary>
    TakeOver,

    /// <summary>
    /// The caller pressed keys on their phone.
    /// </summary>
    KeysPressed,

    /// <summary>
    /// A turn the caller takes without speaking, such as the outcome of a secure keypad entry, which the session
    /// answers like anything the caller said.
    /// </summary>
    CallerTurn,

    /// <summary>
    /// Switch the live session to another AI profile, whose id is the command's text.
    /// </summary>
    SwitchProfile,
}
