using System.Text.Json;
using CrestApps.Core;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Handlers;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Json;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Customers.Handlers;

/// <summary>
/// Adds the Contact step to an order's checkout, and when the order is paid, links it to its customer: the signed-in
/// buyer's, or a new guest customer made from what the guest entered.
/// </summary>
/// <remarks>
/// Whether the step applies is decided from the checkout itself, not the request: a payment provider's webhook and
/// the reconciliation sweep complete checkouts with no signed-in user, and must see the steps the buyer saw.
/// </remarks>
public sealed class CustomerContactCheckoutHandler : CheckoutHandlerBase
{
    private readonly ISiteService _siteService;
    private readonly ICustomerService _customerService;
    private readonly ICustomerStore _customerStore;
    private readonly IOrderStore _orderStore;
    private readonly DocumentJsonSerializerOptions _documentJsonSerializerOptions;

    internal readonly IStringLocalizer S;

    public CustomerContactCheckoutHandler(
        ISiteService siteService,
        ICustomerService customerService,
        ICustomerStore customerStore,
        IOrderStore orderStore,
        IOptions<DocumentJsonSerializerOptions> documentJsonSerializerOptions,
        IStringLocalizer<CustomerContactCheckoutHandler> stringLocalizer)
    {
        _siteService = siteService;
        _customerService = customerService;
        _customerStore = customerStore;
        _orderStore = orderStore;
        _documentJsonSerializerOptions = documentJsonSerializerOptions.Value;
        S = stringLocalizer;
    }

    public override async Task ActivatingAsync(CheckoutFlowActivatingContext context)
    {
        var session = context.Session;

        if (!IsOrder(session))
        {
            return;
        }

        // Staff at a register attach the buyer, when they're known, to the order itself.
        if (session is CheckoutSession { ReferenceId.Length: > 0 } checkout && OrderChannels.IsCounterSale(await _orderStore.FindByIdAsync(checkout.ReferenceId)))
        {
            return;
        }

        // The checkout keeps the setting it started with, so changing it never strands a checkout half way.
        var settings = await _siteService.GetSettingsAsync<CustomerCheckoutSettings>();

        global::OrchardCore.Entities.EntityExtensions.Put(session, new CustomerCheckoutSettings { BillingAddress = settings.BillingAddress });

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = CustomersConstants.ContactStepKey,
            Title = S["Contact"],
            Description = S["Who the order is for, and where it is billed."],
            Order = 1,
            CollectData = true,
            Conceal = IsConcealed(session),
        });
    }

    public override Task InitializingAsync(CheckoutFlowInitializingContext context)
    {
        foreach (var step in context.Flow.Session.Steps)
        {
            if (string.Equals(step.Key, CustomersConstants.ContactStepKey, StringComparison.Ordinal))
            {
                step.Conceal = IsConcealed(context.Flow.Session);
            }
        }

        return Task.CompletedTask;
    }

    public override async Task CompletingAsync(CheckoutFlowCompletingContext context)
    {
        if (context.Flow.Session is not CheckoutSession session || !IsOrder(session) || string.IsNullOrEmpty(session.ReferenceId))
        {
            return;
        }

        var order = await _orderStore.FindByIdAsync(session.ReferenceId);

        if (order is null)
        {
            return;
        }

        var step = GetStep(session);
        Customer customer = null;

        if (!string.IsNullOrEmpty(order.CustomerId))
        {
            customer = await _customerStore.FindByIdAsync(order.CustomerId);
        }
        else if (!string.IsNullOrEmpty(session.OwnerId))
        {
            customer = await _customerService.GetOrCreateForUserAsync(session.OwnerId);
        }
        else if (!string.IsNullOrWhiteSpace(step?.Email))
        {
            // A guest becomes a customer of their own; typing an email never joins someone else's.
            customer = await _customerService.CreateGuestAsync(step.Name, step.Email, step.Phone);
        }

        if (customer is null)
        {
            return;
        }

        order.CustomerId = customer.ItemId;

        await _orderStore.UpdateAsync(order);

        // A guest's billing address is theirs to keep; a signed-in customer's is kept when they asked for it.
        var isGuest = string.IsNullOrEmpty(customer.UserId);

        if (step?.BillingAddress is not null &&
            string.IsNullOrEmpty(step.BillingAddressId) &&
            (isGuest || step.SaveBillingAddress) &&
            (customer.Addresses?.Count ?? 0) < CustomerAddressBook.MaxAddresses &&
            !(customer.Addresses ?? []).Any(entry => IsSameAddress(entry.Address, step.BillingAddress)))
        {
            var entry = new CustomerAddress
            {
                AddressId = UniqueId.GenerateId(),
                Address = step.BillingAddress,
            };

            customer.Addresses ??= [];
            customer.Addresses.Add(entry);
            customer.DefaultBillingAddressId ??= entry.AddressId;
        }

        await _customerService.TouchAsync(customer);
    }

    private CustomerContactStep GetStep(CheckoutSession session)
        => session.SavedSteps.TryGetPropertyValue(CustomersConstants.ContactStepKey, out var node) && node is not null
            ? node.Deserialize<CustomerContactStep>(_documentJsonSerializerOptions.SerializerOptions)
            : null;

    // A signed-in buyer has nothing to enter when no billing address is asked for.
    private static bool IsConcealed(ICheckoutFlowSession session)
        => !string.IsNullOrEmpty(session.OwnerId) && GetRequirement(session) == BillingAddressRequirement.Off;

    internal static BillingAddressRequirement GetRequirement(ICheckoutFlowSession session)
        => global::OrchardCore.Entities.EntityExtensions.TryGet<CustomerCheckoutSettings>(session, out var settings)
            ? settings.BillingAddress
            : BillingAddressRequirement.Optional;

    private static bool IsOrder(ICheckoutFlowSession session)
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
