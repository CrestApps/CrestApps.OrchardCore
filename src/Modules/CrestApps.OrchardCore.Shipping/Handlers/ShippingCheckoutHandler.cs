using System.Text.Json;
using CrestApps.Core;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using Microsoft.Extensions.DependencyInjection;
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
    private const int MaxAddresses = 20;

    private readonly IOrderStore _orderStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;

    internal readonly IStringLocalizer S;

    public ShippingCheckoutHandler(
        IOrderStore orderStore,
        IServiceProvider serviceProvider,
        IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions,
        IStringLocalizer<ShippingCheckoutHandler> stringLocalizer)
    {
        _orderStore = orderStore;
        _serviceProvider = serviceProvider;
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
            _serviceProvider.GetService<ICustomerService>() is not { } customers)
        {
            return;
        }

        var customer = await customers.GetOrCreateForUserAsync(session.OwnerId);

        if (customer is null || (customer.Addresses?.Count ?? 0) >= MaxAddresses || (customer.Addresses ?? []).Any(entry => IsSameAddress(entry.Address, step.Address)))
        {
            return;
        }

        var entry = new CustomerAddress
        {
            AddressId = UniqueId.GenerateId(),
            Address = step.Address,
        };

        customer.Addresses ??= [];
        customer.Addresses.Add(entry);
        customer.DefaultShippingAddressId ??= entry.AddressId;

        await customers.TouchAsync(customer);
    }

    internal static ShippingCheckoutStep GetStep(ICheckoutFlowSession session, DocumentJsonSerializerOptions options)
        => session.SavedSteps.TryGetPropertyValue(ShippingConstants.CheckoutStepKey, out var node) && node is not null
            ? node.Deserialize<ShippingCheckoutStep>(options.SerializerOptions)
            : null;

    internal static bool IsOrder(ICheckoutFlowSession session)
        => session is CheckoutSession checkoutSession &&
            string.Equals(checkoutSession.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal);

    private static bool IsSameAddress(Addresses.Models.Address left, Addresses.Models.Address right)
        => left is not null && right is not null &&
            Same(left.AddressLine1, right.AddressLine1) &&
            Same(left.AddressLine2, right.AddressLine2) &&
            Same(left.City, right.City) &&
            Same(left.Region, right.Region) &&
            Same(left.PostalCode, right.PostalCode) &&
            Same(left.Country, right.Country);

    private static bool Same(string left, string right)
        => string.Equals(left?.Trim() ?? string.Empty, right?.Trim() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
}
