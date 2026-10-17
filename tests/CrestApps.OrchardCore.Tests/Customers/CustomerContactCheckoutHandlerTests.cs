using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Customers.Core;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Handlers;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Json;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Customers;

/// <summary>
/// An order's checkout asks who it is for, and once paid, the order belongs to a customer: the signed-in buyer's, or
/// a guest customer of its own.
/// </summary>
public sealed class CustomerContactCheckoutHandlerTests
{
    private static readonly Address _billing = new() { Name = "Jordan Guest", AddressLine1 = "1 Infinite Loop", City = "Cupertino", Region = "CA", PostalCode = "95014", Country = "US" };

    private readonly Order _order = new() { ItemId = "order-1" };
    private readonly Mock<ICustomerService> _customers = new();
    private readonly Mock<ICustomerStore> _customerStore = new();
    private readonly Mock<IOrderStore> _orders = new();

    public CustomerContactCheckoutHandlerTests()
    {
        _orders.Setup(orders => orders.FindByIdAsync("order-1", It.IsAny<CancellationToken>())).ReturnsAsync(_order);
    }

    private CustomerContactCheckoutHandler CreateHandler(BillingAddressRequirement requirement = BillingAddressRequirement.Optional)
    {
        var site = new Mock<ISite>();
        site.Setup(s => s.GetOrCreate<CustomerCheckoutSettings>()).Returns(new CustomerCheckoutSettings { BillingAddress = requirement });

        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return new CustomerContactCheckoutHandler(
            siteService.Object,
            _customers.Object,
            _customerStore.Object,
            _orders.Object,
            Options.Create(new DocumentJsonSerializerOptions()),
            new PassThroughLocalizer());
    }

    private static CheckoutSession OrderSession(string ownerId = null)
        => new() { SessionId = "session-1", ReferenceType = CheckoutReferenceTypes.Order, ReferenceId = "order-1", OwnerId = ownerId };

    private static void Enter(CheckoutSession session, CustomerContactStep step)
        => session.SavedSteps[CustomersConstants.ContactStepKey] = JObject.FromObject(step);

    [Fact]
    public async Task AnOrder_AsksWhoItIsFor_AndKeepsTheSettingItStartedWith()
    {
        var session = OrderSession();

        await CreateHandler(BillingAddressRequirement.Required).ActivatingAsync(new CheckoutFlowActivatingContext(session));

        var step = Assert.Single(session.Steps);

        Assert.Equal(CustomersConstants.ContactStepKey, step.Key);
        Assert.True(step.CollectData);
        Assert.False(step.Conceal);
        Assert.Equal(BillingAddressRequirement.Required, CustomerContactCheckoutHandler.GetRequirement(session));
    }

    [Fact]
    public async Task ASignedInBuyer_SkipsTheStep_WhenNoBillingAddressIsAsked()
    {
        var session = OrderSession(ownerId: "user-1");

        await CreateHandler(BillingAddressRequirement.Off).ActivatingAsync(new CheckoutFlowActivatingContext(session));

        Assert.True(Assert.Single(session.Steps).Conceal);
    }

    [Fact]
    public async Task OtherPurchases_DoNotGetTheStep()
    {
        var session = new CheckoutSession { SessionId = "session-1", ReferenceType = "Subscription", ReferenceId = "plan-1" };

        await CreateHandler().ActivatingAsync(new CheckoutFlowActivatingContext(session));

        Assert.Empty(session.Steps);
    }

    [Fact]
    public async Task AGuestOrder_BelongsToANewGuestCustomer_WhoKeepsTheBillingAddress()
    {
        var guest = new Customer { ItemId = "guest-1" };

        _customers.Setup(customers => customers.CreateGuestAsync("Jordan Guest", "jordan@example.test", "555-0199", It.IsAny<CancellationToken>())).ReturnsAsync(guest);

        var session = OrderSession();
        Enter(session, new CustomerContactStep { Name = "Jordan Guest", Email = "jordan@example.test", Phone = "555-0199", BillingAddress = _billing });

        await CreateHandler().CompletingAsync(new CheckoutFlowCompletingContext(new CheckoutFlow(session)));

        Assert.Equal("guest-1", _order.CustomerId);

        var entry = Assert.Single(guest.Addresses);

        Assert.Equal("Cupertino", entry.Address.City);
        Assert.Equal(entry.AddressId, guest.DefaultBillingAddressId);
        _customers.Verify(customers => customers.TouchAsync(guest, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ASignedInBuyersOrder_BelongsToTheirCustomer_AndANewAddressIsSavedOnlyWhenAsked()
    {
        var customer = new Customer { ItemId = "customer-1", UserId = "user-1" };

        _customers.Setup(customers => customers.GetOrCreateForUserAsync("user-1", It.IsAny<CancellationToken>())).ReturnsAsync(customer);

        var session = OrderSession(ownerId: "user-1");
        Enter(session, new CustomerContactStep { Name = "Pat Lee", Email = "pat@example.test", BillingAddress = _billing, SaveBillingAddress = false });

        await CreateHandler().CompletingAsync(new CheckoutFlowCompletingContext(new CheckoutFlow(session)));

        Assert.Equal("customer-1", _order.CustomerId);
        Assert.Empty(customer.Addresses ?? []);

        // Asked for, the address is saved once, however often the checkout completes.
        _order.CustomerId = null;
        Enter(session, new CustomerContactStep { Name = "Pat Lee", Email = "pat@example.test", BillingAddress = _billing, SaveBillingAddress = true });

        await CreateHandler().CompletingAsync(new CheckoutFlowCompletingContext(new CheckoutFlow(session)));
        await CreateHandler().CompletingAsync(new CheckoutFlowCompletingContext(new CheckoutFlow(session)));

        Assert.Single(customer.Addresses);
    }

    [Fact]
    public async Task AnOrderAlreadyLinked_KeepsItsCustomer()
    {
        var customer = new Customer { ItemId = "customer-1", UserId = "user-1" };

        _order.CustomerId = "customer-1";
        _customerStore.Setup(store => store.FindByIdAsync("customer-1", It.IsAny<CancellationToken>())).ReturnsAsync(customer);

        var session = OrderSession(ownerId: "user-1");
        Enter(session, new CustomerContactStep { Name = "Pat Lee", Email = "pat@example.test" });

        await CreateHandler().CompletingAsync(new CheckoutFlowCompletingContext(new CheckoutFlow(session)));

        Assert.Equal("customer-1", _order.CustomerId);
        _customers.Verify(customers => customers.GetOrCreateForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _customers.Verify(customers => customers.CreateGuestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class PassThroughLocalizer : IStringLocalizer<CustomerContactCheckoutHandler>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
