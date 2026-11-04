using System.Text.Json;
using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.Shipping.Handlers;

/// <summary>
/// Adds the Shipping step to the checkout of an order with something to ship, and keeps a signed-in buyer's new
/// shipping address in their address book once the order is paid.
/// </summary>
public sealed class ShippingCheckoutHandler : CheckoutHandlerBase
{
    private readonly IOrderStore _orderStore;
    private readonly IEnumerable<IShippingAddressBook> _addressBooks;
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;

    internal readonly IStringLocalizer S;

    public ShippingCheckoutHandler(
        IOrderStore orderStore,
        IEnumerable<IShippingAddressBook> addressBooks,
        IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions,
        IStringLocalizer<ShippingCheckoutHandler> stringLocalizer)
    {
        _orderStore = orderStore;
        _addressBooks = addressBooks;
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
        S = stringLocalizer;
    }

    public override async Task ActivatingAsync(CheckoutFlowActivatingContext context)
    {
        if (context.Session is not CheckoutSession session || !IsOrder(session) || string.IsNullOrEmpty(session.ReferenceId))
        {
            return;
        }

        var order = await _orderStore.FindByIdAsync(session.ReferenceId);

        // An order of downloads and services has nothing to ship, and one sold over a counter is taken away, so its
        // buyer never sees the step.
        if (!ShippingOrderRateService.Ships(order) || OrderChannels.IsCounterSale(order))
        {
            return;
        }

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = ShippingConstants.CheckoutStepKey,
            Title = S["Shipping"],
            Description = S["Where the order goes, and how."],
            Order = 2,
            CollectData = true,
        });
    }

    public override async Task CompletingAsync(CheckoutFlowCompletingContext context)
    {
        if (context.Flow.Session is not CheckoutSession session || !IsOrder(session) || string.IsNullOrEmpty(session.OwnerId))
        {
            return;
        }

        var step = GetStep(session, _documentJsonSerializerOptions);

        if (step is not { SaveAddress: true, Address: not null } || !string.IsNullOrEmpty(step.AddressId) ||
            _addressBooks.LastOrDefault() is not { } addressBook)
        {
            return;
        }

        // The address book refuses the address when it is full or already holds it.
        await addressBook.AddAsync(session.OwnerId, step.Address);
    }

    internal static ShippingCheckoutStep GetStep(ICheckoutFlowSession session, DocumentJsonSerializerOptions options)
        => session.SavedSteps.TryGetPropertyValue(ShippingConstants.CheckoutStepKey, out var node) && node is not null
            ? node.Deserialize<ShippingCheckoutStep>(options.SerializerOptions)
            : null;

    internal static bool IsOrder(ICheckoutFlowSession session)
        => session is CheckoutSession checkoutSession &&
            string.Equals(checkoutSession.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal);
}
