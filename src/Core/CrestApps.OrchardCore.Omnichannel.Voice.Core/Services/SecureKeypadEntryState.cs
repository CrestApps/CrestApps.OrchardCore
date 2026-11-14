namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// What became of keys offered to a secure keypad capture.
/// </summary>
public enum SecureKeypadEntryState
{
    /// <summary>
    /// The keypad is not private on this call: the keys are the caller's turn as usual.
    /// </summary>
    NotCapturing,

    /// <summary>
    /// The keys were kept; the entry is not finished.
    /// </summary>
    Collecting,

    /// <summary>
    /// The entry is finished.
    /// </summary>
    Completed,

    /// <summary>
    /// The caller cancelled the entry.
    /// </summary>
    Cancelled,
}
