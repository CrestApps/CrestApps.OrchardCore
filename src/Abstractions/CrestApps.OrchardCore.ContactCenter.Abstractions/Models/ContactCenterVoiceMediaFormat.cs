namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Describes the audio format exchanged by a live voice media session.
/// </summary>
public sealed class ContactCenterVoiceMediaFormat
{
    /// <summary>
    /// Gets or sets the audio encoding.
    /// </summary>
    public ContactCenterVoiceMediaEncoding Encoding { get; set; }

    /// <summary>
    /// Gets or sets the sample rate in hertz.
    /// </summary>
    public int SampleRate { get; set; }

    /// <summary>
    /// Gets or sets the number of audio channels.
    /// </summary>
    public int Channels { get; set; } = 1;

    /// <summary>
    /// Gets or sets the preferred frame duration in milliseconds.
    /// </summary>
    public int FrameDurationMilliseconds { get; set; } = 20;
}
