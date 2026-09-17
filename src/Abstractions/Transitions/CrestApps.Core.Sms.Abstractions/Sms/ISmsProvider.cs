namespace CrestApps.Core.Sms;

/// <summary>
/// Sends SMS through one carrier.
/// </summary>
public interface ISmsProvider
{
    /// <summary>
    /// Gets the technical name the provider is registered and resolved under.
    /// </summary>
    /// <remarks>
    /// A stable identifier, not a display name: it is stored on a channel endpoint to record which
    /// carrier owns a number, so changing it orphans those endpoints.
    /// </remarks>
    string Name { get; }

    /// <summary>
    /// Sends the message.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether the provider accepted it, and its identifier for it when it reported one.</returns>
    Task<SmsResult> SendAsync(SmsMessage message, CancellationToken cancellationToken = default);
}
