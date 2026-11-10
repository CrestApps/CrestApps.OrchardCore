using CrestApps.OrchardCore.Webhooks.Models;

namespace CrestApps.OrchardCore.Webhooks;

/// <summary>
/// Adds the headers and signature a receiver's protocol asks for to a direct webhook delivery, each time it is sent.
/// </summary>
public interface IWebhookDeliveryFormatter
{
    /// <summary>
    /// Gets the formatter's name, which <see cref="WebhookDirectDelivery.FormatterName"/> names.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Returns the headers to send the delivery with, or why it can't be sent.
    /// </summary>
    /// <param name="context">The delivery, and when it is sent.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task<WebhookFormattedRequest> FormatAsync(WebhookFormatContext context, CancellationToken cancellationToken = default);
}
