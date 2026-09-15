namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Represents one ordered frame of audio exchanged with a live provider call.
/// </summary>
public sealed class ContactCenterVoiceMediaFrame
{
    /// <summary>
    /// Gets or sets the monotonically increasing frame sequence number.
    /// </summary>
    public long SequenceNumber { get; set; }

    /// <summary>
    /// Gets or sets the audio payload in the session's negotiated format.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; set; }
}
