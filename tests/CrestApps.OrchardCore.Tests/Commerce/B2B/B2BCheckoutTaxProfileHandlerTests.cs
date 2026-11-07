using System.Security.Claims;
using CrestApps.OrchardCore.Accounts.B2B.Handlers;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Handlers;
using CrestApps.OrchardCore.Taxation.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Tests.Commerce.B2B;

/// <summary>
/// A buyer who buys for a business account is taxed as a business, with the account's tax ID, whichever handler runs
/// first, and keeps the exemption on their customer record.
/// </summary>
public sealed class B2BCheckoutTaxProfileHandlerTests
{
    private const string BuyerId = "buyer-1";

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ABusinessAccountBuyer_IsTaxedAsABusiness_WithTheAccountsTaxId(bool b2bFirst)
    {
        // Arrange
        var account = TestB2BAccounts.Create(configure: part => part.TaxId = "GB123456789", members: [new AccountMember { UserId = BuyerId }]);
        var b2b = new B2BCheckoutTaxProfileHandler(Accounts(account));
        var customer = new CustomerCheckoutTaxProfileHandler(Customers(new Customer { ItemId = "c1", UserId = BuyerId, Kind = CustomerKind.Person, IsTaxExempt = true }), new ServiceCollection().BuildServiceProvider());
        var provider = new DefaultCheckoutTaxProfileProvider(b2bFirst ? [b2b, customer] : [customer, b2b]);

        // Act
        var profile = await provider.GetProfileAsync(new CheckoutFlow(new CheckoutSession { SessionId = "s1", OwnerId = BuyerId }), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CustomerTaxType.B2B, profile.Customer.CustomerType);
        Assert.Equal("GB123456789", profile.Customer.TaxRegistrationNumber);
        Assert.Equal("GB123456789", profile.Customer.VatNumber);
        Assert.Equal("c1", profile.Customer.CustomerId);
        Assert.True(profile.Customer.IsTaxExempt);
    }

    [Fact]
    public async Task ASaleAtARegister_IsNotTaxedAsTheCashiersAccount()
    {
        var account = TestB2BAccounts.Create(configure: part => part.TaxId = "GB123456789", members: [new AccountMember { UserId = BuyerId }]);
        var provider = new DefaultCheckoutTaxProfileProvider([new B2BCheckoutTaxProfileHandler(Accounts(account))]);
        var session = new CheckoutSession { SessionId = "s1", OwnerId = BuyerId };

        session.Put(new CheckoutCounterSale());

        var profile = await provider.GetProfileAsync(new CheckoutFlow(session), TestContext.Current.CancellationToken);

        Assert.Null(profile.Customer);
    }

    [Fact]
    public async Task ABuyerWithoutABusinessAccount_IsLeftAsTheyAre()
    {
        var provider = new DefaultCheckoutTaxProfileProvider([new B2BCheckoutTaxProfileHandler(Accounts(null))]);

        var profile = await provider.GetProfileAsync(new CheckoutFlow(new CheckoutSession { SessionId = "s1", OwnerId = BuyerId }), TestContext.Current.CancellationToken);

        Assert.Null(profile.Customer);
    }

    private static B2BAccountService Accounts(B2BAccount account)
    {
        var store = new Mock<IB2BAccountStore>();
        store.Setup(value => value.FindByBuyerAsync(BuyerId, It.IsAny<CancellationToken>())).ReturnsAsync(account);

        var httpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, BuyerId)], "test")) };

        return new B2BAccountService(store.Object, new HttpContextAccessor { HttpContext = httpContext });
    }

    private static ICustomerStore Customers(Customer customer)
    {
        var customers = new Mock<ICustomerStore>();
        customers.Setup(value => value.FindByUserIdAsync(BuyerId, It.IsAny<CancellationToken>())).ReturnsAsync(customer);

        return customers.Object;
    }
}
