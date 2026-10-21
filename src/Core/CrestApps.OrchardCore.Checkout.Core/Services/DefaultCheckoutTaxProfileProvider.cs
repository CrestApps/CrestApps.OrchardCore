using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout.Services;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The default <see cref="ICheckoutTaxProfileProvider"/>. It resolves the tax classification a checkout
/// stored on its invoice and where the order is taxed (the address it ships to, else the billing address), then
/// lets every <see cref="ICheckoutTaxProfileHandler"/> add what it knows, such as the customer's exemption or the
/// store's origin. Register a custom implementation to source the profile from your own checkout data instead.
/// </summary>
public sealed class DefaultCheckoutTaxProfileProvider : ICheckoutTaxProfileProvider
{
    private readonly IEnumerable<ICheckoutTaxProfileHandler> _handlers;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCheckoutTaxProfileProvider"/> class.
    /// </summary>
    /// <param name="handlers">The handlers that add what other modules know, such as the customer's exemption.</param>
    public DefaultCheckoutTaxProfileProvider(IEnumerable<ICheckoutTaxProfileHandler> handlers = null)
    {
        _handlers = handlers ?? [];
    }

    /// <inheritdoc/>
    public async Task<CheckoutTaxProfile> GetProfileAsync(CheckoutFlow flow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(flow);

        var profile = new CheckoutTaxProfile
        {
            Destination = GetDestination(flow.Session),
        };

        // Reuse a classification already resolved on the invoice when one exists.
        if (flow.Session.TryGet<CheckoutInvoice>(out var invoice))
        {
            profile.DefaultTaxCategoryCode = invoice.TaxCategoryCode;
            profile.DefaultTaxClassificationCode = invoice.TaxClassificationCode;
        }

        await RunHandlersAsync(profile, flow.Session, cancellationToken);

        return profile;
    }

    /// <inheritdoc/>
    public async Task<CheckoutTaxProfile> GetProfileAsync(ICheckoutFlowSession session, CancellationToken cancellationToken = default)
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

        await RunHandlersAsync(profile, session, cancellationToken);

        return profile;
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

    // What other modules know, such as the customer's exemption or where the store sells from, is added in turn.
    private async Task RunHandlersAsync(CheckoutTaxProfile profile, ICheckoutFlowSession session, CancellationToken cancellationToken)
    {
        foreach (var handler in _handlers)
        {
            await handler.BuildingAsync(profile, session, cancellationToken);
        }
    }
}
