using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Accounts.B2B.Services;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Customers.Core.Models;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.Tests.Commerce.B2B;
using CrestApps.OrchardCore.Tests.Customers;
using CrestApps.OrchardCore.Users;
using CrestApps.OrchardCore.Users.Core.Services;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Tests.Parties;

/// <summary>
/// Each module that keeps a kind of party fills its billing profile: users, customers, accounts (with business buying
/// adding its tax number, billing e-mail and terms) and CRM contacts.
/// </summary>
public sealed class PartyBillingProfileSourcesTests
{
    private static readonly Address _office = new() { AddressLine1 = "1 Main St", City = "Seattle", Region = "WA", PostalCode = "98101", Country = "US" };
    private static readonly Address _warehouse = new() { AddressLine1 = "9 Dock Rd", City = "Tacoma", Region = "WA", PostalCode = "98402", Country = "US" };

    [Fact]
    public async Task AUser_IsBilledUnderTheirDisplayName_AtTheirEmail_WithAnE164Phone()
    {
        // Arrange
        var users = new Mock<IUserSearchService>();
        users
            .Setup(service => service.GetAsync(It.Is<IEnumerable<string>>(ids => ids.Contains("u1")), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new UserSearchResult { UserId = "u1", UserName = "jdoe", DisplayName = "Jane Doe", Email = "jane@example.com", PhoneNumber = "+12065550100" }]);

        var profile = new PartyBillingProfile { Party = new PartyRef(PartyKinds.User, "u1") };

        // Act
        await new UserBillingProfileSource(users.Object).ContributeAsync(profile.Party, profile, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Jane Doe", profile.Contact.Name);
        Assert.Equal("jane@example.com", profile.Contact.Email);
        Assert.Equal("+12065550100", profile.Contact.Phone.Value);
        Assert.Null(profile.Contact.Address);
    }

    /// <summary>
    /// A phone typed as "+1 206 555 0100" is not in E.164 form; it used to be dropped from the billing contact.
    /// </summary>
    [Fact]
    public async Task AUserPhoneThatIsNotInE164Form_IsNormalized_NotDropped()
    {
        // Arrange
        var users = new Mock<IUserSearchService>();
        users
            .Setup(service => service.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new UserSearchResult { UserId = "u1", UserName = "jdoe", Email = "jane@example.com", PhoneNumber = "+1 206 555 0100" }]);

        var profile = new PartyBillingProfile { Party = new PartyRef(PartyKinds.User, "u1") };

        // Act
        await new UserBillingProfileSource(users.Object, PhoneNumbers("+1 206 555 0100", null, "+12065550100")).ContributeAsync(profile.Party, profile, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("+12065550100", profile.Contact.Phone.Value);
    }

    /// <summary>
    /// A customer's phone typed without its country code is read with the country of their billing address.
    /// </summary>
    [Fact]
    public async Task ACustomerPhoneWithoutACountryCode_IsNormalizedWithTheBillingCountry()
    {
        // Arrange
        var customers = new InMemoryCustomerStore();
        customers.Customers.Add(new Customer
        {
            ItemId = "c1",
            FirstName = "Sam",
            LastName = "Lee",
            Phone = "(206) 555-0111",
            Addresses = [new CustomerAddress { AddressId = "home", Address = _office }],
            DefaultBillingAddressId = "home",
        });

        var profile = new PartyBillingProfile();

        // Act
        await new CustomerBillingProfileSource(customers, PhoneNumbers("(206) 555-0111", "US", "+12065550111")).ContributeAsync(new PartyRef(PartyKinds.Customer, "c1"), profile, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("+12065550111", profile.Contact.Phone.Value);
    }

    private static IPhoneNumberService PhoneNumbers(string typed, string region, string e164)
    {
        var service = new Mock<IPhoneNumberService>();
        var formatted = e164;
        service.Setup(candidate => candidate.TryFormatToE164(typed, region, out formatted)).Returns(true);

        return service.Object;
    }

    [Fact]
    public async Task AUserThatIsNotFound_LeavesTheProfileAsItIs()
    {
        var users = new Mock<IUserSearchService>();
        users.Setup(service => service.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var profile = new PartyBillingProfile();

        await new UserBillingProfileSource(users.Object).ContributeAsync(new PartyRef(PartyKinds.User, "gone"), profile, TestContext.Current.CancellationToken);

        Assert.False(profile.Contact.HasAnyValue);
    }

    [Fact]
    public async Task ACustomer_IsBilledFromTheirRecord()
    {
        // Arrange
        var customers = new InMemoryCustomerStore();
        customers.Customers.Add(new Customer
        {
            ItemId = "c1",
            Kind = CustomerKind.Business,
            CompanyName = "Fabrikam",
            Email = "ap@fabrikam.test",
            Phone = "+12065550111",
            TaxId = " DE123 ",
            IsTaxExempt = true,
            Addresses =
            [
                new CustomerAddress { AddressId = "billing", Address = _office },
                new CustomerAddress { AddressId = "shipping", Address = _warehouse },
            ],
            DefaultBillingAddressId = "billing",
            DefaultShippingAddressId = "shipping",
        });

        var profile = new PartyBillingProfile();

        // Act
        await new CustomerBillingProfileSource(customers).ContributeAsync(new PartyRef(PartyKinds.Customer, "c1"), profile, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Fabrikam", profile.Contact.Name);
        Assert.Equal("ap@fabrikam.test", profile.Contact.Email);
        Assert.Equal("+12065550111", profile.Contact.Phone.Value);
        Assert.Equal("Seattle", profile.Contact.Address.City);
        Assert.Equal("Tacoma", profile.ShippingAddress.City);
        Assert.Equal("DE123", profile.TaxId);
        Assert.True(profile.TaxExempt);
    }

    [Fact]
    public async Task ACustomerShippedWhereTheyAreBilled_HasNoSeparateShippingAddress()
    {
        var customers = new InMemoryCustomerStore();
        customers.Customers.Add(new Customer
        {
            ItemId = "c1",
            FirstName = "Sam",
            LastName = "Lee",
            Addresses = [new CustomerAddress { AddressId = "home", Address = _office }],
            DefaultBillingAddressId = "home",
            DefaultShippingAddressId = "home",
        });

        var profile = new PartyBillingProfile();

        await new CustomerBillingProfileSource(customers).ContributeAsync(new PartyRef(PartyKinds.Customer, "c1"), profile, TestContext.Current.CancellationToken);

        Assert.Equal("Sam Lee", profile.Contact.Name);
        Assert.Null(profile.ShippingAddress);
        Assert.False(profile.TaxExempt);
        Assert.Null(profile.TaxId);
    }

    [Fact]
    public void AnAccount_IsBilledAtItsMarkedLocation_ElseItsFirst()
    {
        var marked = new AccountLocationsPart { Locations = [null, new AccountLocation { LocationId = "hq", Address = _office }, new AccountLocation { LocationId = "wh", Address = _warehouse, IsBilling = true }] };
        var unmarked = new AccountLocationsPart { Locations = [null, new AccountLocation { LocationId = "hq", Address = _office }, new AccountLocation { LocationId = "wh", Address = _warehouse }] };

        Assert.Equal("wh", marked.GetBillingLocation().LocationId);
        Assert.Equal("hq", unmarked.GetBillingLocation().LocationId);
        Assert.Null(new AccountLocationsPart().GetBillingLocation());
        Assert.Null(new AccountLocationsPart { Locations = null }.GetBillingLocation());
    }

    [Fact]
    public async Task AnAccount_IsBilledUnderItsName_AtItsBillingLocation()
    {
        // Arrange
        var account = TestB2BAccounts.CreateItem(
            name: "Fabrikam Ltd",
            locations: [new AccountLocation { LocationId = "hq", Address = _office }, new AccountLocation { LocationId = "wh", Address = _warehouse, IsBilling = true }]);

        var profile = new PartyBillingProfile();

        // Act
        await new AccountBillingProfileSource(ContentManager(account)).ContributeAsync(new PartyRef(PartyKinds.Account, account.ContentItemId), profile, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Fabrikam Ltd", profile.Contact.Name);
        Assert.Equal("Tacoma", profile.Contact.Address.City);
        Assert.NotSame(_warehouse, profile.Contact.Address);
    }

    [Fact]
    public async Task AContentItemThatIsNotAnAccount_IsNotBilledAsOne()
    {
        var page = new ContentItem { ContentItemId = "page-1", ContentType = "Page", DisplayText = "About us" };
        var profile = new PartyBillingProfile();

        await new AccountBillingProfileSource(ContentManager(page)).ContributeAsync(new PartyRef(PartyKinds.Account, "page-1"), profile, TestContext.Current.CancellationToken);

        Assert.Null(profile.Contact.Name);
    }

    [Fact]
    public async Task BusinessBuying_AddsTheTaxNumber_TheBillingEmail_AndTheTerms()
    {
        // Arrange
        var account = TestB2BAccounts.Create(configure: part =>
        {
            part.TaxId = " GB123456789 ";
            part.BillingEmail = "invoices@fabrikam.test";
            part.Terms = new() { NetDays = 30, CreditLimit = 5000m };
        });

        var profile = new PartyBillingProfile { Contact = ContactInfo.From("Fabrikam Ltd", null, null) };

        // Act
        await new B2BAccountBillingProfileSource(B2BStore(account)).ContributeAsync(new PartyRef(PartyKinds.Account, account.AccountId), profile, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("GB123456789", profile.TaxId);
        Assert.Equal("invoices@fabrikam.test", profile.Contact.Email);
        Assert.Empty(profile.CopyTo);
        Assert.Equal(30, profile.NetDays);
        Assert.Equal(5000m, profile.CreditLimit);
    }

    [Fact]
    public async Task BusinessBuying_CopiesTheBillingEmail_WhenTheAccountHasAnEmail_AndKeepsTermsOff_WhileItIsOff()
    {
        // Arrange
        var account = TestB2BAccounts.Create(configure: part =>
        {
            part.Enabled = false;
            part.TaxId = "GB123456789";
            part.BillingEmail = "invoices@fabrikam.test";
            part.Terms = new() { NetDays = 30, CreditLimit = 5000m };
        });

        var profile = new PartyBillingProfile { Contact = ContactInfo.From("Fabrikam Ltd", "office@fabrikam.test", null) };

        // Act
        await new B2BAccountBillingProfileSource(B2BStore(account)).ContributeAsync(new PartyRef(PartyKinds.Account, account.AccountId), profile, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("office@fabrikam.test", profile.Contact.Email);
        Assert.Equal(["invoices@fabrikam.test"], profile.CopyTo);
        Assert.Equal("GB123456789", profile.TaxId);
        Assert.Null(profile.NetDays);
        Assert.Null(profile.CreditLimit);
    }

    [Fact]
    public async Task AnAccountAndItsBusinessBuying_MergeInOneProfile_ThroughTheService()
    {
        // Arrange
        var item = TestB2BAccounts.CreateItem(
            configure: part => part.TaxId = "GB123456789",
            locations: [new AccountLocation { LocationId = "hq", Address = _office }]);

        var service = new PartyBillingProfileService(
            [new AccountBillingProfileSource(ContentManager(item)), new B2BAccountBillingProfileSource(B2BStore(new(item)))],
            new PartyDirectory([]));

        // Act
        var profile = await service.GetAsync(new PartyRef(PartyKinds.Account, item.ContentItemId), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Fabrikam Ltd", profile.Contact.Name);
        Assert.Equal("Seattle", profile.Contact.Address.City);
        Assert.Equal("GB123456789", profile.TaxId);
    }

    [Fact]
    public async Task AContact_IsBilledUnderItsName_AtItsEmail_PhoneAndAddressField()
    {
        // Arrange
        var contact = new ContentItem { ContentItemId = "contact-1", ContentType = "Contact", DisplayText = "Alex Kim", Published = true, Latest = true };
        contact.Weld<OmnichannelContactPart>();

        // The contact type's ContactDetails part holds an address field named Mailing.
        contact.Content["ContactDetails"] = new JsonObject
        {
            ["Mailing"] = JsonSerializer.SerializeToNode(new AddressField { Address = _office }),
        };

        var methods = new Mock<IContactMethodsAccessor>();
        methods
            .Setup(accessor => accessor.Get(contact))
            .Returns(new ContactMethodSet(
                [new ContactPhoneNumber { PhoneNumber = "+12065550123", Type = ContactPhoneTypes.Cell }],
                [new ContactEmailAddress { Email = "alex@example.com" }]));

        var definitions = new Mock<IContentDefinitionManager>();
        definitions
            .Setup(manager => manager.GetTypeDefinitionAsync("Contact"))
            .ReturnsAsync(ContactType());

        var source = new ContactBillingProfileSource(ContentManager(contact), definitions.Object, methods.Object);
        var profile = new PartyBillingProfile();

        // Act
        await source.ContributeAsync(new PartyRef(PartyKinds.Contact, "contact-1"), profile, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Alex Kim", profile.Contact.Name);
        Assert.Equal("alex@example.com", profile.Contact.Email);
        Assert.Equal("+12065550123", profile.Contact.Phone.Value);
        Assert.Equal("Seattle", profile.Contact.Address.City);
    }

    /// <summary>
    /// A contact type may have several address fields. The billing address is the first one, in the order the editor
    /// shows them (part position, then field position), that has an address; not whichever the definition stored first.
    /// </summary>
    [Fact]
    public async Task AContactWithSeveralAddressFields_IsBilledAtTheFirstInEditorOrder()
    {
        // Arrange
        var contact = new ContentItem { ContentItemId = "contact-1", ContentType = "Contact", DisplayText = "Alex Kim", Published = true, Latest = true };
        contact.Weld<OmnichannelContactPart>();
        contact.Content["ContactDetails"] = new JsonObject
        {
            ["Mailing"] = JsonSerializer.SerializeToNode(new AddressField { Address = _warehouse }),
            ["Billing"] = JsonSerializer.SerializeToNode(new AddressField { Address = _office }),
        };

        var methods = new Mock<IContactMethodsAccessor>();
        methods.Setup(accessor => accessor.Get(contact)).Returns(new ContactMethodSet([], []));

        var mailing = new ContentPartFieldDefinition(new ContentFieldDefinition(nameof(AddressField)), "Mailing", Position("5"));
        var billing = new ContentPartFieldDefinition(new ContentFieldDefinition(nameof(AddressField)), "Billing", Position("2"));
        var part = new ContentPartDefinition("ContactDetails", [mailing, billing], []);
        var type = new ContentTypeDefinition("Contact", "Contact", [new ContentTypePartDefinition("ContactDetails", part, [])], []);

        foreach (var typePart in type.Parts)
        {
            typePart.ContentTypeDefinition = type;
        }

        var definitions = new Mock<IContentDefinitionManager>();
        definitions.Setup(manager => manager.GetTypeDefinitionAsync("Contact")).ReturnsAsync(type);

        var profile = new PartyBillingProfile();

        // Act
        await new ContactBillingProfileSource(ContentManager(contact), definitions.Object, methods.Object)
            .ContributeAsync(new PartyRef(PartyKinds.Contact, "contact-1"), profile, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Seattle", profile.Contact.Address.City);
    }

    private static JsonObject Position(string position)
        => new()
        {
            ["ContentPartFieldSettings"] = new JsonObject { ["Position"] = position },
        };

    private static ContentTypeDefinition ContactType()
    {
        var addressField = new ContentPartFieldDefinition(new ContentFieldDefinition(nameof(AddressField)), "Mailing", []);
        var part = new ContentPartDefinition("ContactDetails", [addressField], []);
        var type = new ContentTypeDefinition("Contact", "Contact", [new ContentTypePartDefinition("ContactDetails", part, [])], []);

        foreach (var typePart in type.Parts)
        {
            typePart.ContentTypeDefinition = type;
        }

        return type;
    }

    private static IContentManager ContentManager(ContentItem contentItem)
    {
        var contentManager = new Mock<IContentManager>();
        contentManager
            .Setup(manager => manager.GetAsync(contentItem.ContentItemId, It.IsAny<VersionOptions>()))
            .ReturnsAsync(contentItem);

        return contentManager.Object;
    }

    private static IB2BAccountStore B2BStore(B2BAccount account)
    {
        var store = new Mock<IB2BAccountStore>();
        store
            .Setup(value => value.FindByIdAsync(account.AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account);

        return store.Object;
    }
}
