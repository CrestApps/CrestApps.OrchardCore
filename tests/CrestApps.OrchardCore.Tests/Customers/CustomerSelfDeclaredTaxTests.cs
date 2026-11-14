using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Mvc.Localization;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Customers;

/// <summary>
/// SEC-C4 (review): the plan's test that a shopper can't verify their own tax number. The profile a shopper saves (the
/// account page, and PUT me over the API, which build the same model) never carries the flag, and a changed number
/// must be verified again.
/// </summary>
public sealed class CustomerSelfDeclaredTaxTests
{
    [Fact]
    public async Task AShopperWhoDeclaresABusiness_IsNotVerified()
    {
        var customer = new Customer { ItemId = "c1", UserId = "user-1", Kind = CustomerKind.Person };

        await CreatePages().SaveProfileAsync(customer, new MyAccountViewModel { Kind = CustomerKind.Business, CompanyName = "Contoso", TaxId = "DE123" }, new ModelStateDictionary(), TestContext.Current.CancellationToken);

        Assert.Equal(CustomerKind.Business, customer.Kind);
        Assert.False(customer.TaxIdVerified);
    }

    [Fact]
    public async Task AVerifiedBusinessThatChangesItsNumber_IsNoLongerVerified()
    {
        var customer = new Customer { ItemId = "c1", UserId = "user-1", Kind = CustomerKind.Business, CompanyName = "Contoso", TaxId = "DE123", TaxIdVerified = true };
        var pages = CreatePages();

        await pages.SaveProfileAsync(customer, new MyAccountViewModel { Kind = CustomerKind.Business, CompanyName = "Contoso", TaxId = "DE123" }, new ModelStateDictionary(), TestContext.Current.CancellationToken);
        Assert.True(customer.TaxIdVerified);

        await pages.SaveProfileAsync(customer, new MyAccountViewModel { Kind = CustomerKind.Business, CompanyName = "Contoso", TaxId = "DE999" }, new ModelStateDictionary(), TestContext.Current.CancellationToken);
        Assert.False(customer.TaxIdVerified);
    }

    private static CustomerAccountPages CreatePages()
    {
        var localizer = new Mock<IStringLocalizer<CustomerAccountPages>>();
        localizer.Setup(value => value[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));

        var services = new ServiceCollection()
            .AddSingleton(Mock.Of<ICustomerService>())
            .AddSingleton(Mock.Of<ICustomerStore>())
            .AddSingleton(Mock.Of<Microsoft.AspNetCore.Authorization.IAuthorizationService>())
            .AddSingleton(Mock.Of<global::OrchardCore.DisplayManagement.Notify.INotifier>())
            .AddSingleton(Mock.Of<IClock>(clock => clock.UtcNow == new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc)))
            .AddSingleton(localizer.Object)
            .AddSingleton(Mock.Of<IHtmlLocalizer<CustomerAccountPages>>())
            .BuildServiceProvider();

        // The address book isn't reached by a profile save.
        return ActivatorUtilities.CreateInstance<CustomerAccountPages>(services, (CustomerAddressBook)RuntimeHelpers.GetUninitializedObject(typeof(CustomerAddressBook)));
    }
}
