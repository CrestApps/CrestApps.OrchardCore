using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Customers.ViewModels;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Tests.Customers;

/// <summary>
/// A customer keeps their addresses, with one billed and one shipped to first; an order keeps its own copy, so
/// changing or deleting an address never changes it.
/// </summary>
public sealed class CustomerAddressBookTests
{
    private static CustomerAddressViewModel Home(bool billing = false, bool shipping = false) => new()
    {
        Label = " Home ",
        Name = "Pat Lee",
        AddressLine1 = "1 Main St",
        City = "Springfield",
        Region = "IL",
        PostalCode = "62701",
        Country = "us",
        IsDefaultBilling = billing,
        IsDefaultShipping = shipping,
    };

    [Fact]
    public void ANewAddress_IsAdded_AndCanBecomeTheDefaults()
    {
        var customer = new Customer();

        var address = CustomerAddressBook.Apply(customer, null, Home(billing: true, shipping: true));

        Assert.Same(address, Assert.Single(customer.Addresses));
        Assert.Equal("Home", address.Label);
        Assert.Equal("US", address.Country);
        Assert.Same(address, customer.GetDefaultBillingAddress());
        Assert.Same(address, customer.GetDefaultShippingAddress());
    }

    [Fact]
    public void MakingAnotherAddressTheDefault_MovesIt_AndUntickingClearsIt()
    {
        var customer = new Customer();
        var home = CustomerAddressBook.Apply(customer, null, Home(billing: true, shipping: true));
        var office = CustomerAddressBook.Apply(customer, null, Home(billing: true));

        Assert.Same(office, customer.GetDefaultBillingAddress());
        Assert.Same(home, customer.GetDefaultShippingAddress());

        // Unticking the default on the address that is the default leaves none; unticking it elsewhere changes nothing.
        CustomerAddressBook.Apply(customer, home, Home(billing: false, shipping: false));

        Assert.Same(office, customer.GetDefaultBillingAddress());
        Assert.Null(customer.GetDefaultShippingAddress());
    }

    [Fact]
    public void DeletingAnAddress_ClearsTheDefaultsThatNamedIt()
    {
        var customer = new Customer();
        var home = CustomerAddressBook.Apply(customer, null, Home(billing: true, shipping: true));

        Assert.True(customer.RemoveAddress(home.AddressId));
        Assert.Empty(customer.Addresses);
        Assert.Null(customer.DefaultBillingAddressId);
        Assert.Null(customer.DefaultShippingAddressId);
        Assert.False(customer.RemoveAddress(home.AddressId));
    }

    [Fact]
    public void AnOrdersCopy_DoesNotChangeWhenTheAddressDoes()
    {
        var customer = new Customer();
        var home = CustomerAddressBook.Apply(customer, null, Home());

        var snapshot = home.ToAddress();
        home.City = "Chicago";

        Assert.Equal("Springfield", snapshot.City);
    }

    [Fact]
    public void AnAddress_ReadsLikeAnEnvelope()
    {
        var address = new CustomerAddress { Name = "Pat Lee", AddressLine1 = "1 Main St", City = "Springfield", Region = "IL", PostalCode = "62701", Country = "US" };

        Assert.Equal(["Pat Lee", "1 Main St", "Springfield IL 62701", "United States"], address.GetLines(code => code == "US" ? "United States" : code));
    }

    [Fact]
    public async Task AnAddress_NeedsAName_AStreet_ACity_AndAKnownCountry()
    {
        var book = new CustomerAddressBook(new DefaultCountryService(), new PassThroughLocalizer());
        var modelState = new ModelStateDictionary();

        await book.ValidateAsync(new Customer(), new CustomerAddressViewModel { Country = "ZZ" }, isNew: true, modelState);

        Assert.Equal(
            [nameof(CustomerAddressViewModel.AddressLine1), nameof(CustomerAddressViewModel.City), nameof(CustomerAddressViewModel.Country), nameof(CustomerAddressViewModel.Name)],
            modelState.Where(entry => entry.Value.Errors.Count > 0).Select(entry => entry.Key).Order());

        modelState.Clear();
        await book.ValidateAsync(new Customer(), Home(), isNew: true, modelState);

        Assert.True(modelState.IsValid);
    }

    [Fact]
    public async Task AFullAddressBook_TakesNoMore()
    {
        var book = new CustomerAddressBook(new DefaultCountryService(), new PassThroughLocalizer());
        var customer = new Customer();

        for (var i = 0; i < CustomerAddressBook.MaxAddresses; i++)
        {
            CustomerAddressBook.Apply(customer, null, Home());
        }

        var modelState = new ModelStateDictionary();
        await book.ValidateAsync(customer, Home(), isNew: true, modelState);

        Assert.False(modelState.IsValid);

        // Editing one of them is still allowed.
        modelState.Clear();
        await book.ValidateAsync(customer, Home(), isNew: false, modelState);

        Assert.True(modelState.IsValid);
    }

    [Fact]
    public async Task ANewAddress_StartsWithTheCustomersDetails_WithoutRepeatingABusinessName()
    {
        var book = new CustomerAddressBook(new DefaultCountryService(), new PassThroughLocalizer());

        var person = await book.BuildEditorAsync(new Customer { FirstName = "Pat", LastName = "Lee", Phone = "555-0100" }, null);

        Assert.Equal("Pat Lee", person.Name);
        Assert.Null(person.Company);
        Assert.Equal("555-0100", person.Phone);
        Assert.True(person.IsDefaultBilling);
        Assert.True(person.IsDefaultShipping);

        // A business with no contact name leaves the recipient for the user to fill in, rather than repeating the company.
        var business = await book.BuildEditorAsync(new Customer { Kind = CustomerKind.Business, CompanyName = "Contoso" }, null);

        Assert.Null(business.Name);
        Assert.Equal("Contoso", business.Company);
    }

    private sealed class PassThroughLocalizer : IStringLocalizer<CustomerAddressBook>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
