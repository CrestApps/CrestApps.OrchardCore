using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Services;

/// <summary>
/// Implemented by an SMS provider that can send picture messages (MMS). The built-in <see cref="ISmsProvider"/>
/// carries text only, so the dispatcher sends pictures only through a provider that implements this, and refuses
/// them for any other.
/// </summary>
public interface ISmsMediaDispatchProvider
{
    /// <summary>
    /// Sends the message with the pictures at the specified links and reports the provider's identifier for it.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="mediaUrls">The public links the provider downloads the pictures from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The dispatch outcome, carrying the provider message identifier when the send was accepted.</returns>
    Task<MessageDispatchResult> DispatchAsync(SmsMessage message, IReadOnlyList<string> mediaUrls, CancellationToken cancellationToken = default);
}
