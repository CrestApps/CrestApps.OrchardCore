namespace CrestApps.OrchardCore.Asterisk.Models;

/// <summary>
/// Represents the raw bytes of an Asterisk ARI stored recording downloaded through the stored-file endpoint,
/// together with the reported media content type.
/// </summary>
internal sealed class AsteriskAriStoredRecordingContent
{
    /// <summary>
    /// Gets or sets the raw, unencrypted recording bytes as returned by Asterisk.
    /// </summary>
    public byte[] Content { get; set; }

    /// <summary>
    /// Gets or sets the media content type Asterisk reported for the downloaded recording, when present.
    /// </summary>
    public string ContentType { get; set; }
}
