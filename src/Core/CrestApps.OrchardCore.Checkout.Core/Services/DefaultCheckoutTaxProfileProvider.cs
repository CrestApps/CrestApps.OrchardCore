using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout.Services;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The default <see cref="ICheckoutTaxProfileProvider"/>. It resolves the tax classification a checkout
/// stored on its invoice and leaves address/customer resolution empty so no tax is applied unless a
/// scenario-specific provider supplies real origin/destination data. Register a custom implementation to
/// source a full billing/shipping address and customer profile from your own checkout data (for example a
/// subscription content item or a goods-purchase address step).
/// </summary>
public sealed class DefaultCheckoutTaxProfileProvider : ICheckoutTaxProfileProvider
{
    /// <inheritdoc/>
    public Task<CheckoutTaxProfile> GetProfileAsync(CheckoutFlow flow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(flow);

        var profile = new CheckoutTaxProfile
        {
            Destination = GetDestination(flow.Session),
        };

        // Reuse a classification already resolved on the invoice when one exists; a scenario-specific
        // provider supplies the customer profile.
        if (flow.Session.TryGet<CheckoutInvoice>(out var invoice))
        {
            profile.DefaultTaxCategoryCode = invoice.TaxCategoryCode;
            profile.DefaultTaxClassificationCode = invoice.TaxClassificationCode;
        }

        return Task.FromResult(profile);
    }

    /// <inheritdoc/>
    public Task<CheckoutTaxProfile> GetProfileAsync(ICheckoutFlowSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var profile = new CheckoutTaxProfile
        {
            Destination = GetDestination(session),
        };

        // The originating content is not available at recurring billing time, so the classification
        // resolved at checkout is reused from the persisted invoice. A scenario-specific provider can
        // re-resolve the destination so a customer address change takes effect on future cycles.
        if (session.TryGet<CheckoutInvoice>(out var invoice))
        {
            profile.DefaultTaxCategoryCode = invoice.TaxCategoryCode;
            profile.DefaultTaxClassificationCode = invoice.TaxClassificationCode;
        }

        return Task.FromResult(profile);
    }

    /// <summary>
    /// Gets where the order is taxed: the address it ships to, else the buyer's billing address, else nowhere (only
    /// rules without a jurisdiction then apply).
    /// </summary>
    public static Address GetDestination(ICheckoutFlowSession session)
    {
        if (session.TryGet<CheckoutShippingInfo>(out var shipping) && shipping.Address is { } shippingAddress)
        {
            return shippingAddress;
        }

        return session.TryGet<CheckoutContactInfo>(out var contact) ? contact.BillingAddress : null;
    }
}
