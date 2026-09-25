namespace CrestApps.OrchardCore.Telephony.Models;

/// <summary>
/// Represents a request to transfer an active call to another destination.
/// </summary>
public sealed class TransferRequest
{
    /// <summary>
    /// Gets or sets the identifier of the call to transfer.
    /// </summary>
    public string CallId { get; set; }

    /// <summary>
    /// Gets or sets the destination phone number or address to transfer the call to. When
    /// <see cref="IsExtension"/> is <see langword="true"/>, this is the internal extension number.
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="To"/> is an internal extension rather than a phone
    /// number. An extension is resolved to the user it rings and reached the way an extension call reaches them,
    /// never dialed as a phone number.
    /// </summary>
    public bool IsExtension { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user the extension rings. Set by the telephony service once it has
    /// resolved the extension, so the provider can reach that user's own endpoint.
    /// </summary>
    public string TargetUserId { get; set; }

    /// <summary>
    /// Gets the extension this request transfers to: <see cref="To"/>, trimmed, when <see cref="IsExtension"/> is
    /// <see langword="true"/> and it is one to fifteen digits.
    /// </summary>
    /// <returns>The extension, or <see langword="null"/> when the request names no valid extension.</returns>
    public string GetExtension()
    {
        if (!IsExtension || string.IsNullOrWhiteSpace(To))
        {
            return null;
        }

        var extension = To.Trim();

        return extension.Length <= 15 && extension.All(char.IsAsciiDigit)
            ? extension
            : null;
    }

    /// <summary>
    /// Gets or sets the transfer mode that controls whether the agent speaks to the destination
    /// before completing the transfer.
    /// </summary>
    public TransferMode Mode { get; set; }
}
