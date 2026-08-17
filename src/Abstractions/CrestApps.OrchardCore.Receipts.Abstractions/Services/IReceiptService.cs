using CrestApps.OrchardCore.Receipts.Models;

namespace CrestApps.OrchardCore.Receipts.Services;

/// <summary>
/// Builds printable receipt documents from consumer-supplied purchase data, merging in the tenant's
/// configured issuer branding. This is the single reusable entry point any module uses to produce a
/// receipt, so branding and layout stay consistent across subscriptions, e-commerce, and future modules.
/// </summary>
public interface IReceiptService
{
    /// <summary>
    /// Builds a fully-resolved receipt document from a request, merging the configured
    /// <see cref="ReceiptSettings"/> and computing the subtotal.
    /// </summary>
    /// <param name="request">The purchase data the consumer knows about.</param>
    /// <returns>The printable receipt document.</returns>
    ValueTask<ReceiptDocument> BuildAsync(ReceiptRequest request);
}
