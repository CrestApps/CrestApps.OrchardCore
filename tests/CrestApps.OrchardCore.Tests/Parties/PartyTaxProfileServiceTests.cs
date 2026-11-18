using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Taxation.Core.Services;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Commerce.B2B;
using CrestApps.OrchardCore.Tests.Customers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Parties;

/// <summary>
/// A document billed to a party is taxed with the party's exemption, tax number and business status, read from its
/// billing profile and the modules that know more, without a checkout.
/// </summary>
public sealed class PartyTaxProfileServiceTests
{
    private static readonly Address _office = new() { AddressLine1 = "1 Main St", City = "Seattle", Region = "WA", PostalCode = "98101", Country = "US" };

    [Fact]
    public async Task AnExemptCustomer_IsTaxedAsExempt_WithTheirCertificates_AtTheirAddress()
    {
        // Arrange
        var customers = new InMemoryCustomerStore();
        customers.Customers.Add(new Customer
        {
            ItemId = "c1",
            Kind = CustomerKind.Person,
            FirstName = "Sam",
            IsTaxExempt = true,
            Addresses = [new CustomerAddress { AddressId = "home", Address = _office }],
            DefaultBillingAddressId = "home",
        });

        var certificates = new Mock<IExemptionCertificateStore>();
        certificates
            .Setup(store => store.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ExemptionCertificate { ItemId = "cert-1", CustomerId = "c1" }, new ExemptionCertificate { ItemId = "cert-2", CustomerId = "other" }]);

        var serviceProvider = new ServiceCollection().AddSingleton(certificates.Object).BuildServiceProvider();
        var service = CreateService(
            [new CustomerBillingProfileSource(customers)],
            new CustomerPartyTaxProfileProvider(customers, serviceProvider));

        // Act
        var profile = await service.GetAsync(new PartyRef(PartyKinds.Customer, "c1"), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(profile.IsTaxExempt);
        Assert.Equal("c1", profile.CustomerId);
        Assert.Equal(CustomerTaxType.B2C, profile.CustomerType);
        Assert.Equal(["cert-1"], profile.ExemptionCertificateIds);
        Assert.Equal("Seattle", profile.ResidenceAddress.City);
        Assert.Null(profile.BusinessAddress);
        Assert.Null(profile.VatNumber);
    }

    [Fact]
    public async Task ABusinessCustomer_IsTaxedAsABusiness_WithTheirTaxId()
    {
        var customers = new InMemoryCustomerStore();
        customers.Customers.Add(new Customer { ItemId = "c1", Kind = CustomerKind.Business, CompanyName = "Fabrikam", TaxId = "DE123" });

        var service = CreateService(
            [new CustomerBillingProfileSource(customers)],
            new CustomerPartyTaxProfileProvider(customers, new ServiceCollection().BuildServiceProvider()));

        var profile = await service.GetAsync(new PartyRef(PartyKinds.Customer, "c1"), TestContext.Current.CancellationToken);

        Assert.Equal(CustomerTaxType.B2B, profile.CustomerType);
        Assert.Equal("DE123", profile.TaxRegistrationNumber);
        Assert.Equal("DE123", profile.VatNumber);
        Assert.False(profile.IsTaxExempt);
    }

    [Fact]
    public async Task ABusinessAccountWithATaxNumber_IsTaxedAsABusiness_AtItsBillingLocation()
    {
        // Arrange
        var item = TestB2BAccounts.CreateItem(
            configure: part => part.TaxId = " GB123456789 ",
            locations: [new AccountLocation { LocationId = "hq", Address = _office, IsBilling = true }]);

        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(manager => manager.GetAsync(item.ContentItemId, It.IsAny<VersionOptions>()))
            .ReturnsAsync(item);

        var store = new Mock<IB2BAccountStore>();
        store
            .Setup(value => value.FindByIdAsync(item.ContentItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new B2BAccount(item));

        var service = CreateService([new AccountBillingProfileSource(contentManager.Object), new B2BAccountBillingProfileSource(store.Object)]);

        // Act
        var profile = await service.GetAsync(new PartyRef(PartyKinds.Account, item.ContentItemId), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CustomerTaxType.B2B, profile.CustomerType);
        Assert.Equal("GB123456789", profile.TaxRegistrationNumber);
        Assert.Equal("GB123456789", profile.VatNumber);
        Assert.False(profile.IsTaxExempt);
        Assert.Equal("Seattle", profile.BusinessAddress.City);
        Assert.Null(profile.ResidenceAddress);
        Assert.Null(profile.CustomerId);
    }

    [Theory]
    [InlineData(PartyKinds.User, "u1")]
    [InlineData(PartyKinds.Contact, "contact-1")]
    [InlineData(null, null)]
    public async Task APartyWithNoData_IsTaxedAsAPerson_WithNoTaxNumberOrExemption(string kind, string id)
    {
        var service = CreateService([]);

        var profile = await service.GetAsync(new PartyRef(kind, id), TestContext.Current.CancellationToken);

        Assert.NotNull(profile);
        Assert.Equal(CustomerTaxType.B2C, profile.CustomerType);
        Assert.Null(profile.TaxRegistrationNumber);
        Assert.Null(profile.VatNumber);
        Assert.False(profile.IsTaxExempt);
        Assert.Null(profile.ResidenceAddress);
        Assert.Null(profile.BusinessAddress);
        Assert.Empty(profile.ExemptionCertificateIds);
    }

    [Fact]
    public void SetTaxNumber_TrimsTheNumberIntoBothFields_AndIgnoresABlankOne()
    {
        var profile = new CustomerTaxProfile { TaxRegistrationNumber = "KEEP", VatNumber = "KEEP" };

        profile.SetTaxNumber("  ");
        Assert.Equal("KEEP", profile.VatNumber);

        profile.SetTaxNumber(" GB1 ");
        Assert.Equal("GB1", profile.TaxRegistrationNumber);
        Assert.Equal("GB1", profile.VatNumber);
    }

    private static PartyTaxProfileService CreateService(IPartyBillingProfileSource[] sources, params IPartyTaxProfileProvider[] providers)
    {
        var billing = new PartyBillingProfileService(sources, new PartyDirectory([]));

        return new PartyTaxProfileService([new BillingProfilePartyTaxProfileProvider(billing), .. providers]);
    }
}
