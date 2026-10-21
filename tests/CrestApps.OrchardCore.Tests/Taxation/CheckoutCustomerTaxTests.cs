using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Handlers;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Taxation;

/// <summary>
/// Who is buying reaches checkout tax: a tax-exempt business pays no tax, a business carries its tax id, a guest is
/// taxed as a person, and the store's origin is on the profile.
/// </summary>
public sealed class CheckoutCustomerTaxTests
{
    [Fact]
    public async Task ATaxExemptBusinessCustomer_IsChargedNoTax()
    {
        var harness = await HarnessAsync();
        var exempt = await TaxAsync(harness, new Customer { ItemId = "c1", UserId = "user-1", Kind = CustomerKind.Business, CompanyName = "Contoso Resale", TaxId = "RS-1001", IsTaxExempt = true });
        var regular = await TaxAsync(harness, new Customer { ItemId = "c2", UserId = "user-1", Kind = CustomerKind.Person });

        Assert.Equal(0m, exempt.TaxAmount);
        Assert.Equal(10m, regular.TaxAmount);
    }

    [Fact]
    public async Task ABusinessWithACertificateOnFile_IsExemptForWhatItCovers()
    {
        var harness = await HarnessAsync();
        await harness.Exemptions.CreateAsync(new ExemptionCertificate { ItemId = "cert-1", Name = "WA resale", CustomerId = "c3", Status = ExemptionStatus.Active }, TestContext.Current.CancellationToken);

        var result = await TaxAsync(harness, new Customer { ItemId = "c3", UserId = "user-1", Kind = CustomerKind.Business }, harness.Exemptions);

        Assert.Equal(0m, result.TaxAmount);
    }

    [Fact]
    public async Task TheProfile_SaysWhoIsBuying_AndWhereTheStoreSellsFrom()
    {
        var customers = new Mock<ICustomerStore>();
        customers.Setup(value => value.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Customer { ItemId = "c1", UserId = "user-1", Kind = CustomerKind.Business, TaxId = "DE123", IsTaxExempt = false });

        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<TaxOriginSettings>()).Returns(new TaxOriginSettings { Origin = new Address { Country = "US", Region = "WA", City = "Seattle", PostalCode = "98101" } });
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        var provider = new DefaultCheckoutTaxProfileProvider(
        [
            new CustomerCheckoutTaxProfileHandler(customers.Object, new ServiceCollection().BuildServiceProvider()),
            new TaxOriginCheckoutTaxProfileHandler(siteService.Object),
        ]);

        var signedIn = await provider.GetProfileAsync(new CheckoutFlow(new CheckoutSession { SessionId = "s1", OwnerId = "user-1" }), TestContext.Current.CancellationToken);
        var guest = await provider.GetProfileAsync(new CheckoutFlow(new CheckoutSession { SessionId = "s2" }), TestContext.Current.CancellationToken);

        Assert.Equal(CustomerTaxType.B2B, signedIn.Customer.CustomerType);
        Assert.Equal("DE123", signedIn.Customer.VatNumber);
        Assert.Equal("WA", signedIn.Origin.Region);
        Assert.Null(guest.Customer);
        Assert.Equal("US", guest.Origin.Country);
    }

    private static async Task<TaxHarnessResult> TaxAsync(TaxTestHarness harness, Customer customer, IExemptionCertificateStore certificates = null)
    {
        var customers = new Mock<ICustomerStore>();
        customers.Setup(value => value.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>())).ReturnsAsync(customer);

        var services = new ServiceCollection();

        if (certificates is not null)
        {
            services.AddSingleton(certificates);
        }

        var session = new CheckoutSession { SessionId = "session-1", OwnerId = "user-1" };
        session.Put(new CheckoutShippingInfo { Address = new Address { Country = "US", Region = "WA", City = "Seattle", PostalCode = "98101", AddressLine1 = "400 Pine St" } });

        var provider = new DefaultCheckoutTaxProfileProvider([new CustomerCheckoutTaxProfileHandler(customers.Object, services.BuildServiceProvider())]);
        var profile = await provider.GetProfileAsync(new CheckoutFlow(session), TestContext.Current.CancellationToken);

        var invoice = new CheckoutInvoice
        {
            Currency = "USD",
            InitialPaymentAmount = 100m,
            DueNow = 100m,
            LineItems = [new CheckoutLineItem { ItemId = "line-1", Quantity = 1, UnitPrice = 100m, TaxableKind = TaxableItemKind.Physical }],
        };

        var result = await harness.TaxService.CalculateAsync(CheckoutTaxContextFactory.Create(invoice, profile, TaxTestData.TransactionDate), TestContext.Current.CancellationToken);

        return new TaxHarnessResult(result.TaxAmount);
    }

    private static async Task<TaxTestHarness> HarnessAsync()
    {
        var harness = new TaxTestHarness(new TestClock(TaxTestData.TransactionDate));
        var washington = await TaxTestData.AddJurisdictionAsync(harness, "Washington", "US", "WA");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "Washington sales tax",
            TaxType = TaxTypeNames.SalesTax,
            Source = "Percentage",
            JurisdictionId = washington,
            Rate = 0.10m,
        });

        return harness;
    }

    private sealed record TaxHarnessResult(decimal TaxAmount);
}
