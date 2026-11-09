using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;

namespace CrestApps.OrchardCore.Tests.Shipping;

/// <summary>
/// Shipping lists and saves the buyer's addresses through the Addresses contract, which Customers implements, so
/// Shipping never locates the customer service.
/// </summary>
public sealed class ShippingAddressBookTests
{
    private static readonly Address _home = new() { Name = "Ann Lee", Country = "US", Region = "WA", City = "Seattle", AddressLine1 = "9 Pine Rd" };

    [Fact]
    public async Task TheShippingStep_ReadsTheBuyersAddressBookThroughTheContract_AndNoneForAGuest()
    {
        // Arrange
        var addressBook = new Mock<IShippingAddressBook>();
        var book = new ShippingAddressBook
        {
            Addresses = [new ShippingAddressBookEntry { AddressId = "a1", Address = _home }],
            DefaultShippingAddressId = "a1",
            MaxAddresses = 20,
        };

        addressBook.Setup(value => value.GetAsync("user-1", It.IsAny<CancellationToken>())).ReturnsAsync(book);

        var step = new ShippingCheckoutStepService(null, null, null, null, [], [addressBook.Object], null, new PassThroughStringLocalizer<ShippingCheckoutStepService>());

        // Act
        var signedIn = await step.GetAddressBookAsync(new CheckoutSession { OwnerId = "user-1" }, TestContext.Current.CancellationToken);
        var guest = await step.GetAddressBookAsync(new CheckoutSession(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(book, signedIn);
        Assert.Equal("a1", signedIn.GetDefaultShippingAddress().AddressId);
        Assert.True(signedIn.CanAdd);
        Assert.Null(guest);
    }

    [Fact]
    public async Task WithoutAnAddressBookModule_TheShippingStepHasNoAddressBook()
    {
        var step = new ShippingCheckoutStepService(null, null, null, null, [], [], null, new PassThroughStringLocalizer<ShippingCheckoutStepService>());

        Assert.Null(await step.GetAddressBookAsync(new CheckoutSession { OwnerId = "user-1" }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheCustomersAddressBook_SavesANewAddressOnce_AndMakesTheFirstTheDefault()
    {
        // Arrange
        var customer = new Customer { ItemId = "c1", UserId = "user-1" };
        var customers = new Mock<ICustomerService>();
        customers.Setup(service => service.GetOrCreateForUserAsync("user-1", It.IsAny<CancellationToken>())).ReturnsAsync(customer);

        var addressBook = new CustomerShippingAddressBook(customers.Object);

        // Act
        var added = await addressBook.AddAsync("user-1", _home, TestContext.Current.CancellationToken);
        var again = await addressBook.AddAsync("user-1", new Address { Name = "Ann Lee", Country = "US", Region = "WA", City = "Seattle", AddressLine1 = "9 Pine Rd" }, TestContext.Current.CancellationToken);
        var book = await addressBook.GetAsync("user-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(added);
        Assert.False(again);
        var entry = Assert.Single(book.Addresses);
        Assert.Equal(entry.AddressId, book.DefaultShippingAddressId);
        Assert.Equal(CustomerAddressBook.MaxAddresses, book.MaxAddresses);
        customers.Verify(service => service.TouchAsync(customer, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheCustomersAddressBook_RefusesAnAddressWhenItIsFull()
    {
        var customer = new Customer
        {
            ItemId = "c1",
            Addresses = [.. Enumerable.Range(0, CustomerAddressBook.MaxAddresses).Select(i => new CustomerAddress { AddressId = $"a{i}", Address = new Address { Country = "US", AddressLine1 = $"{i} Main St" } })],
        };
        var customers = new Mock<ICustomerService>();
        customers.Setup(service => service.GetOrCreateForUserAsync("user-1", It.IsAny<CancellationToken>())).ReturnsAsync(customer);

        var added = await new CustomerShippingAddressBook(customers.Object).AddAsync("user-1", _home, TestContext.Current.CancellationToken);

        Assert.False(added);
        Assert.Equal(CustomerAddressBook.MaxAddresses, customer.Addresses.Count);
    }
}
