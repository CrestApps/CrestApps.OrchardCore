namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// Sends the store's emails to its customers, such as an order's confirmation, a booking or a gift card: written in the
/// email's culture, passed through the store's template of its kind and the store's layout, and sent, with a send that
/// fails logged.
/// </summary>
public interface ICommerceEmailSender
{
    /// <summary>
    /// Gets a value indicating whether the site can send email.
    /// </summary>
    bool CanSend { get; }

    /// <summary>
    /// Builds the email in its culture and sends it. Returns whether it went: <see langword="false"/> when the site can't
    /// send email, the email has no recipient, the build gives nothing to send, or the send fails, which is logged. A
    /// send that fails never throws.
    /// </summary>
    /// <param name="email">Who the email goes to, what it's about, and the culture it's written in.</param>
    /// <param name="buildAsync">Builds the email's content, in the email's culture; returns <see langword="null"/> to send
    /// nothing.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<bool> SendAsync(CommerceEmail email, Func<CancellationToken, Task<CommerceEmailContent>> buildAsync, CancellationToken cancellationToken = default);
}
