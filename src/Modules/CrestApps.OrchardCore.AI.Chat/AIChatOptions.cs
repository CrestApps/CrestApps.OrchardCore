namespace CrestApps.OrchardCore.AI.Chat;

/// <summary>
/// Configuration options for AI Chat functionality.
/// </summary>
public sealed class AIChatOptions
{
    /// <summary>
    /// Gets or sets the maximum allowed size for base64-encoded audio data in bytes.
    /// When null or negative, no size limit is enforced.
    /// Default is 10MB (10,000,000 bytes), which corresponds to approximately 7.5MB of raw audio
    /// or about 5 minutes of audio at 24kbps bitrate.
    /// </summary>
    public long? MaxAudioSizeInBytes { get; set; } = 10_000_000;
}
