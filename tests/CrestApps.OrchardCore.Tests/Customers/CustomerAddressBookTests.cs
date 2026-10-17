using CrestApps.OrchardCore.Addresses.Core.Editing;
using CrestApps.OrchardCore.Addresses.Core.Services;
using CrestApps.OrchardCore.Addresses.Models;
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
    private static readonly Address _home = new()
    {
        Name = "Pat Lee",
        AddressLine1 = "1 Main St",
        City = "Springfield",
        Region = "IL",
        PostalCode = "62701",
        Country = "US",
    };

    private static CustomerAddressViewModel Entry(bool billing = false, bool shipping = false) => new()
    {
        Label = " Home ",
        IsDefaultBilling = billing,
        IsDefaultShipping = shipping,
    };

    private static AddressEditorViewModel Posted(string country = "US", string region = "Illinois") => new()
    {
        Name = "Pat Lee",
        AddressLine1 = "1 Main St",
        City = "Springfield",
        Region = region,
        PostalCode = "62701",
        Country = country,
    };

    private static CustomerAddressBook CreateBook()
    {
        var formats = new DefaultAddressFormatProvider();
        var countries = new DefaultCountryService();
        var editor = new DefaultAddressEditorService(formats, new DefaultAddressValidator(formats, countries), countries, new PassThroughLocalizer<DefaultAddressEditorService>());

        return new CustomerAddressBook(editor, new DefaultAddressFormatter(formats, countries), shapeFactory: null, new PassThroughLocalizer<CustomerAddressBook>());
    }

    [Fact]
    public void ANewAddress_IsAdded_AndCanBecomeTheDefaults()
    {
        var customer = new Customer();

        var entry = CustomerAddressBook.Apply(customer, null, Entry(billing: true, shipping: true), _home);

        Assert.Same(entry, Assert.Single(customer.Addresses));
        Assert.Equal("Home", entry.Label);
        Assert.Same(_home, entry.Address);
        Assert.Same(entry, customer.GetDefaultBillingAddress());
        Assert.Same(entry, customer.GetDefaultShippingAddress());
    }

    [Fact]
    public void MakingAnotherAddressTheDefault_MovesIt_AndUntickingClearsIt()
    {
        var customer = new Customer();
        var home = CustomerAddressBook.Apply(customer, null, Entry(billing: true, shipping: true), _home);
        var office = CustomerAddressBook.Apply(customer, null, Entry(billing: true), _home);

        Assert.Same(office, customer.GetDefaultBillingAddress());
        Assert.Same(home, customer.GetDefaultShippingAddress());

        // Unticking the default on the address that is the default leaves none; unticking it elsewhere changes nothing.
        CustomerAddressBook.Apply(customer, home, Entry(), _home);

        Assert.Same(office, customer.GetDefaultBillingAddress());
        Assert.Null(customer.GetDefaultShippingAddress());
    }

    [Fact]
    public void DeletingAnAddress_ClearsTheDefaultsThatNamedIt()
    {
        var customer = new Customer();
        var home = CustomerAddressBook.Apply(customer, null, Entry(billing: true, shipping: true), _home);

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
        var home = CustomerAddressBook.Apply(customer, null, Entry(), _home);

        var copy = home.ToAddress();

        CustomerAddressBook.Apply(customer, home, Entry(), new Address { City = "Chicago", Country = "US" });

        Assert.Equal("Springfield", copy.City);
        Assert.NotSame(home.Address, copy);
    }

    [Fact]
    public async Task AnAddress_IsCheckedAgainstItsCountry_AndNeedsARecipient()
    {
        var modelState = new ModelStateDictionary();

        var value = await CreateBook().ValidateAsync(new Customer(), new CustomerAddressViewModel { Address = new AddressEditorViewModel { Country = "US", City = "Springfield" } }, isNew: true, modelState);

        Assert.Null(value);
        Assert.Equal(
            ["Address.AddressLine1", "Address.Name", "Address.PostalCode", "Address.Region"],
            modelState.Where(entry => entry.Value.Errors.Count > 0).Select(entry => entry.Key).Order());
    }

    [Fact]
    public async Task AValidAddress_ComesBackTidied_WithTheRegionByItsCode()
    {
        var modelState = new ModelStateDictionary();

        var value = await CreateBook().ValidateAsync(new Customer(), new CustomerAddressViewModel { Address = Posted() }, isNew: true, modelState);

        Assert.True(modelState.IsValid);
        Assert.Equal("IL", value.Region);
    }

    [Fact]
    public async Task AFullAddressBook_TakesNoMore()
    {
        var book = CreateBook();
        var customer = new Customer();

        for (var i = 0; i < CustomerAddressBook.MaxAddresses; i++)
        {
            CustomerAddressBook.Apply(customer, null, Entry(), _home);
        }

        var modelState = new ModelStateDictionary();

        Assert.Null(await book.ValidateAsync(customer, new CustomerAddressViewModel { Address = Posted() }, isNew: true, modelState));
        Assert.False(modelState.IsValid);

        // Editing one of them is still allowed.
        modelState.Clear();

        Assert.NotNull(await book.ValidateAsync(customer, new CustomerAddressViewModel { Address = Posted() }, isNew: false, modelState));
    }

    [Fact]
    public void ANewAddress_StartsInTheCountryOfTheCustomersOtherAddresses()
    {
        var customer = new Customer();

        CustomerAddressBook.Apply(customer, null, Entry(billing: true), new Address { Country = "CA", City = "Toronto" });

        Assert.Equal("CA", CustomerAddressBook.EditorOptionsFor(customer).DefaultCountry);
        Assert.Null(CustomerAddressBook.EditorOptionsFor(new Customer()).DefaultCountry);
        Assert.True(CustomerAddressBook.EditorOptionsFor(customer).RequireName);
    }

    [Fact]
    public void ANewAddress_StartsWithTheCustomersDetails_WithoutRepeatingABusinessName()
    {
        var person = CustomerAddressBook.NewAddressFor(new Customer { FirstName = "Pat", LastName = "Lee", Phone = "555-0100" });

        Assert.Equal("Pat Lee", person.Name);
        Assert.Null(person.Company);
        Assert.Equal("555-0100", person.Phone);

        // A business with no contact name leaves the recipient for the user to fill in, rather than repeating the company.
        var business = CustomerAddressBook.NewAddressFor(new Customer { Kind = CustomerKind.Business, CompanyName = "Contoso" });

        Assert.Null(business.Name);
        Assert.Equal("Contoso", business.Company);
    }

    [Fact]
    public async Task TheAddressList_PrintsEachAddressTheWayItsCountryWritesIt()
    {
        var customer = new Customer();

        CustomerAddressBook.Apply(customer, null, Entry(billing: true), new Address { Name = "Pat Lee", AddressLine1 = "Unter den Linden 1", City = "Berlin", PostalCode = "10117", Country = "DE" });

        var summary = Assert.Single(await CreateBook().SummarizeAsync(customer));

        Assert.Equal(["Pat Lee", "Unter den Linden 1", "10117 Berlin", "Germany"], summary.Lines);
        Assert.True(summary.IsDefaultBilling);
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
