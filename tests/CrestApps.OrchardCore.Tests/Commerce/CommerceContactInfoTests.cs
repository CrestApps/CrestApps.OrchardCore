using System.Text.Json;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using Moq;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Commerce;

/// <summary>
/// Commerce keeps a buyer's phone in E.164 form when it parses, so it matches CRM, do-not-call and caller-ID numbers,
/// and keeps what was typed when it doesn't; contacts are shared as <see cref="ContactInfo"/>.
/// </summary>
public sealed class CommerceContactInfoTests
{
    private static readonly IPhoneNumberService _phoneNumbers = new DefaultPhoneNumberService();

    [Theory]
    [InlineData("+1 (702) 499-3350", null, "+17024993350")]
    [InlineData("(702) 499-3350", "US", "+17024993350")]
    [InlineData("  +17024993350 ", null, "+17024993350")]
    [InlineData("(702) 499-3350", null, "(702) 499-3350")]
    [InlineData(" call me at the office ", "US", "call me at the office")]
    [InlineData("   ", "US", null)]
    [InlineData(null, "US", null)]
    public void APhone_IsKeptInE164_WhenItParses_AndAsTypedWhenItDoesnt(string typed, string region, string expected)
        => Assert.Equal(expected, PhoneNumberNormalization.ToE164OrTrimmed(typed, _phoneNumbers, region));

    [Fact]
    public void WithoutThePhoneNumbersFeature_APhoneIsOnlyTrimmed()
        => Assert.Equal("(702) 499-3350", PhoneNumberNormalization.ToE164OrTrimmed(" (702) 499-3350 ", phoneNumberService: null, "US"));

    [Fact]
    public async Task ACustomersPhone_IsSavedInE164_ReadInTheCountryOfTheirAddress()
    {
        var store = new CustomerStore(Mock.Of<ISession>(), [], [_phoneNumbers]);
        var customer = new Customer
        {
            ItemId = "c1",
            Phone = "(702) 499-3350",
            Addresses = [new CustomerAddress { AddressId = "a1", Address = new Address { Country = "US" } }],
            DefaultBillingAddressId = "a1",
        };

        await store.CreateAsync(customer, TestContext.Current.CancellationToken);

        Assert.Equal("+17024993350", customer.Phone);

        customer.Phone = "ext. 12 at the desk";
        await store.UpdateAsync(customer, TestContext.Current.CancellationToken);

        Assert.Equal("ext. 12 at the desk", customer.Phone);
    }

    [Fact]
    public async Task AnOrdersPhone_IsSavedInE164_ReadInTheCountryOfItsAddress()
    {
        var store = new OrderStore(Mock.Of<ISession>(), [_phoneNumbers]);
        var order = new Order
        {
            ItemId = "o1",
            CustomerPhone = " 0207 946 0018 ",
            BillingAddress = new Address { Country = "GB" },
        };

        await store.CreateAsync(order, TestContext.Current.CancellationToken);

        Assert.Equal("+442079460018", order.CustomerPhone);
    }

    [Fact]
    public async Task WithoutThePhoneNumbersFeature_TheStoresKeepThePhoneAsTyped()
    {
        var customer = new Customer { ItemId = "c1", Phone = " (702) 499-3350 " };
        var order = new Order { ItemId = "o1", CustomerPhone = "(702) 499-3350" };

        await new CustomerStore(Mock.Of<ISession>(), []).CreateAsync(customer, TestContext.Current.CancellationToken);
        await new OrderStore(Mock.Of<ISession>()).CreateAsync(order, TestContext.Current.CancellationToken);

        Assert.Equal("(702) 499-3350", customer.Phone);
        Assert.Equal("(702) 499-3350", order.CustomerPhone);
    }

    [Fact]
    public void AContact_KeepsAPhoneOnlyInE164()
    {
        var contact = ContactInfo.From(" Sam Lee ", "sam@example.com", "+17024993350", new Address { Country = "US" });
        var typed = ContactInfo.From("Sam", null, "(702) 499-3350");

        Assert.Equal("Sam Lee", contact.Name);
        Assert.Equal("+17024993350", contact.Phone.Value);
        Assert.False(typed.Phone.HasValue);
        Assert.True(typed.HasAnyValue);
        Assert.False(new ContactInfo().HasAnyValue);
    }

    [Fact]
    public void ACheckoutsContact_IsAContactInfo()
    {
        var contact = new CheckoutContactInfo
        {
            DisplayName = "Sam",
            Email = "sam@example.com",
            Phone = "+17024993350",
            BillingAddress = new Address { City = "Las Vegas" },
        }.ToContactInfo();

        Assert.Equal("Sam", contact.Name);
        Assert.Equal("sam@example.com", contact.Email);
        Assert.Equal("+17024993350", contact.Phone.Value);
        Assert.Equal("Las Vegas", contact.Address.City);
    }

    [Fact]
    public void AContact_RoundTripsAsJson_WithItsPhoneAsE164Text()
    {
        var contact = ContactInfo.From("Sam", "sam@example.com", "+17024993350");

        var json = JsonSerializer.Serialize(contact);
        var read = JsonSerializer.Deserialize<ContactInfo>(json);

        Assert.Equal("+17024993350", JsonDocument.Parse(json).RootElement.GetProperty("Phone").GetString());
        Assert.Equal(contact, read);
    }
}
