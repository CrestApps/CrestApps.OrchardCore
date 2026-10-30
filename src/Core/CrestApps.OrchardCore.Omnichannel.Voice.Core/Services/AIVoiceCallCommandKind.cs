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
}
