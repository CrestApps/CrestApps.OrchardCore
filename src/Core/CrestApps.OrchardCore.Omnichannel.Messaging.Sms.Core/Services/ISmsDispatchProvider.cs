using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using OrchardCore.Sms;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Services;

/// <summary>
/// Implemented by an SMS provider that can report its own identifier for a message it accepted. The built-in
/// <see cref="ISmsProvider"/> result carries no identifier, so a provider that has one implements this in
/// addition and the dispatcher prefers it.
/// </summary>
public interface ISmsDispatchProvider
{
    /// <summary>
    /// Sends the message and reports the provider's identifier for it.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The dispatch outcome, carrying the provider message identifier when the send was accepted.</returns>
    Task<MessageDispatchResult> DispatchAsync(SmsMessage message, CancellationToken cancellationToken = default);
}
