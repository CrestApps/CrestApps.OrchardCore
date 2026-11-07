namespace CrestApps.OrchardCore.Webhooks;

/// <summary>
/// Signs outbound webhook bodies: <c>v1=</c> and the lowercase hex HMAC-SHA256 of <c>{timestamp}.{body}</c> keyed with
/// the subscription's secret, sent in <see cref="WebhookHeaders.Signature"/> with the timestamp in
/// <see cref="WebhookHeaders.Timestamp"/>.
/// </summary>
public interface IWebhookSigner
{
    /// <summary>
    /// Returns the signature of a body signed at a time.
    /// </summary>
    /// <param name="secret">The shared secret.</param>
    /// <param name="timestamp">When it was signed, in seconds since 1970.</param>
    /// <param name="body">The body.</param>
    /// <returns>The <see cref="WebhookHeaders.Signature"/> header value.</returns>
    string Sign(string secret, long timestamp, string body);

    /// <summary>
    /// Returns whether a signature was made with the secret over the body at the timestamp, compared in constant time.
    /// A receiver uses it; it does not check how old the timestamp is.
    /// </summary>
    /// <param name="secret">The shared secret.</param>
    /// <param name="timestamp">The timestamp the sender sent.</param>
    /// <param name="body">The body received.</param>
    /// <param name="signature">The signature the sender sent.</param>
    /// <returns><see langword="true"/> when the signature is valid.</returns>
    bool Verify(string secret, long timestamp, string body, string signature);
}
