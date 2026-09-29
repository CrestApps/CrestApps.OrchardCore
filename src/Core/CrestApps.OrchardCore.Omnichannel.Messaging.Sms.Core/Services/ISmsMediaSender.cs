using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Services;

/// <summary>
/// Sends picture messages (MMS) for an SMS provider whose own <see cref="ISmsProvider"/> carries text only, such as a
/// provider that ships with OrchardCore. The dispatcher prefers a provider that implements
/// <see cref="ISmsMediaDispatchProvider"/> itself, and otherwise uses the sender registered for the provider's name.
/// </summary>
public interface ISmsMediaSender
{
    /// <summary>
    /// Gets the technical name of the SMS provider this sender sends pictures for, as the provider is registered.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Sends the message with the pictures at the specified links.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="mediaUrls">The public links the provider downloads the pictures from.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The dispatch outcome, carrying the provider message identifier when the send was accepted.</returns>
    Task<MessageDispatchResult> SendAsync(SmsMessage message, IReadOnlyList<string> mediaUrls, CancellationToken cancellationToken = default);
}
