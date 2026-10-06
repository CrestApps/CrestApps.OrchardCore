namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The kind of call a <see cref="CallRecording"/> captured.
/// </summary>
public enum CallRecordingSource
{
    /// <summary>
    /// A Contact Center call: routed from an entry point, placed by the dialer, or handed to an agent by an AI.
    /// </summary>
    ContactCenter = 0,

    /// <summary>
    /// A call an automated voice agent handled.
    /// </summary>
    AiAgent = 1,

    /// <summary>
    /// A number an agent dialed on the soft phone's keypad.
    /// </summary>
    SoftPhone = 2,
}
