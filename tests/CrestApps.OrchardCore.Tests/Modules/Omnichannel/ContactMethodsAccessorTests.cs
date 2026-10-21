using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

public sealed class ContactMethodsAccessorTests
{
    private const string PersonType = "Person";

    [Fact]
    public void Get_WhenTheTypeUsesTheBag_ReadsEveryPhoneAndEmailItemTrimmedAndWithItsTypeNormalized()
    {
        // Arrange
        var contact = CreateBagContact(
            "Contact",
            CreatePhoneNumber(" +17025550100 ", " cell "),
            CreatePhoneNumber("   ", "Home"),
            CreateEmailAddress(" lead@example.com "),
            CreatePhoneNumber("+13105550100", "Mobile"),
            CreatePhoneNumber("+12125550100", "HOME", contentType: "FaxNumber"),
            new ContentItem { ContentType = "Note" });

        // Act
        var contactMethods = TestContactMethods.Get(contact);

        // Assert
        Assert.Collection(
            contactMethods.PhoneNumbers,
            phone => AssertPhone(phone, "+17025550100", ContactPhoneTypes.Cell),
            phone => AssertPhone(phone, "+13105550100", ContactPhoneTypes.Cell),
            phone => AssertPhone(phone, "+12125550100", ContactPhoneTypes.Home));
        Assert.Equal("lead@example.com", Assert.Single(contactMethods.EmailAddresses).Email);
    }

    [Fact]
    public void Get_WhenTheContactHasNoBag_ReturnsTheEmptySet()
    {
        // Arrange
        var contact = new ContentItem { ContentType = "Contact" };

        // Act & Assert
        Assert.Same(ContactMethodSet.Empty, TestContactMethods.Get(contact));
        Assert.Same(ContactMethodSet.Empty, TestContactMethods.Get(null));
    }

    [Fact]
    public void Get_WhenTheTypeHasAProvider_ReadsThroughItAndIgnoresAnyBagTheItemStillCarries()
    {
        // Arrange
        var accessor = CreateAccessorWithPersonProvider();
        var person = CreatePerson("+17025550100", "+12125550100", "person@example.com");
        person.Apply(OmnichannelConstants.NamedParts.ContactMethods, CreateBag(CreatePhoneNumber("+19995550100", "Cell")));

        // Act
        var contactMethods = accessor.Get(person);

        // Assert
        Assert.Collection(
            contactMethods.PhoneNumbers,
            phone => AssertPhone(phone, "+17025550100", ContactPhoneTypes.Cell),
            phone => AssertPhone(phone, "+12125550100", ContactPhoneTypes.Home));
        Assert.Equal("person@example.com", contactMethods.GetPrimaryEmailAddress());
    }

    [Fact]
    public void Get_WhenAnotherTypeHasAProvider_StillReadsThisTypeFromTheBag()
    {
        // Arrange
        var accessor = CreateAccessorWithPersonProvider();
        var contact = CreateBagContact("Contact", CreatePhoneNumber("+17025550100", "Cell"));

        // Act
        var contactMethods = accessor.Get(contact);

        // Assert
        AssertPhone(Assert.Single(contactMethods.PhoneNumbers), "+17025550100", ContactPhoneTypes.Cell);
        Assert.True(accessor.UsesContactMethodsBag("Contact"));
        Assert.False(accessor.UsesContactMethodsBag(PersonType));
    }

    [Fact]
    public void AddContactMethodsProvider_WithoutAContentType_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddContactMethodsProvider<FieldContactMethodsProvider>());
        Assert.Throws<ArgumentException>(() => services.AddContactMethodsProvider<FieldContactMethodsProvider>(" "));
    }

    [Fact]
    public void CreateIndex_WhenTheTypeHasAProvider_IndexesTheNumbersAndEmailFromItsOwnFields()
    {
        // Arrange
        var indexProvider = new OmnichannelContactIndexProvider(new DefaultPhoneNumberService(), CreateAccessorWithPersonProvider());
        var person = CreatePerson("(702) 555-0100", "+12125550100", "person@example.com", countryCode: "US");

        // Act
        var index = indexProvider.CreateIndex(person);

        // Assert
        Assert.NotNull(index);
        Assert.Equal("+17025550100", index.NormalizedPrimaryCellPhoneNumber);
        Assert.Equal("7025550100", index.PrimaryCellPhoneNumber);
        Assert.Equal("+12125550100", index.NormalizedPrimaryHomePhoneNumber);
        Assert.Equal("person@example.com", index.PrimaryEmailAddress);
    }

    [Fact]
    public void FindDestination_WhenTheTypeHasAProvider_ReachesTheContactOnItsOwnFields()
    {
        // Arrange
        var accessor = CreateAccessorWithPersonProvider();
        var person = CreatePerson("+17025550100", "+12125550100", "person@example.com");
        var contactMethods = accessor.Get(person);

        // Act & Assert
        Assert.Equal("+17025550100", OmnichannelHelper.FindDestination(contactMethods, OmnichannelConstants.Channels.Phone));
        Assert.Equal("+17025550100", OmnichannelHelper.FindDestination(contactMethods, OmnichannelConstants.Channels.Sms));
        Assert.Equal("person@example.com", OmnichannelHelper.FindDestination(contactMethods, OmnichannelConstants.Channels.Email));
        Assert.Equal(
            "+12125550100",
            OmnichannelHelper.FindDestination(contactMethods, OmnichannelConstants.Channels.Phone, number => number == "+17025550100"));
    }

    [Fact]
    public void GetPreferredPhoneNumber_TakesTheFirstNumberOfTheTypeCalledFirst()
    {
        // Arrange
        var contactMethods = new ContactMethodSet(
            [
                new ContactPhoneNumber { PhoneNumber = "+1", Type = ContactPhoneTypes.Work },
                new ContactPhoneNumber { PhoneNumber = "+2", Type = "Fax" },
                new ContactPhoneNumber { PhoneNumber = "+3", Type = ContactPhoneTypes.Home },
                new ContactPhoneNumber { PhoneNumber = "+4", Type = ContactPhoneTypes.Home },
                new ContactPhoneNumber { PhoneNumber = "+5", Type = ContactPhoneTypes.Other },
            ],
            []);

        // Act & Assert
        Assert.Equal("+3", contactMethods.GetPreferredPhoneNumber()?.PhoneNumber);
        Assert.Equal("+4", contactMethods.GetPreferredPhoneNumber(number => number == "+3")?.PhoneNumber);
        Assert.Equal("+1", contactMethods.GetPreferredPhoneNumber(number => number is "+3" or "+4")?.PhoneNumber);
        Assert.Null(contactMethods.GetPreferredPhoneNumber(number => number != "+2"));
    }

    [Theory]
    [InlineData("Cell", ContactPhoneTypes.Cell)]
    [InlineData(" cell ", ContactPhoneTypes.Cell)]
    [InlineData("MOBILE", ContactPhoneTypes.Cell)]
    [InlineData("home", ContactPhoneTypes.Home)]
    [InlineData("Office", ContactPhoneTypes.Office)]
    [InlineData("work", ContactPhoneTypes.Work)]
    [InlineData("other", ContactPhoneTypes.Other)]
    [InlineData(" Fax ", "Fax")]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void Normalize_SpellsEveryKnownTypeOneWay(string type, string expected)
    {
        Assert.Equal(expected, ContactPhoneTypes.Normalize(type));
    }

    private static IContactMethodsAccessor CreateAccessorWithPersonProvider()
        => TestContactMethods
            .CreateServices(services => services.AddContactMethodsProvider<FieldContactMethodsProvider>(PersonType))
            .GetRequiredService<IContactMethodsAccessor>();

    private static void AssertPhone(ContactPhoneNumber phone, string number, string type)
    {
        Assert.Equal(number, phone.PhoneNumber);
        Assert.Equal(type, phone.Type);
    }

    private static ContentItem CreatePerson(string mobile, string landline, string email, string countryCode = null)
    {
        var person = new ContentItem
        {
            ContentItemId = "person-id",
            ContentType = PersonType,
            Published = true,
            Latest = true,
        };

        person.Alter<OmnichannelContactPart>(_ => { });
        person.Alter<PersonContactPart>(part =>
        {
            part.Mobile = new PhoneField { PhoneNumber = mobile, CountryCode = countryCode };
            part.Landline = new PhoneField { PhoneNumber = landline, CountryCode = countryCode };
            part.Email = new TextField { Text = email };
        });

        return person;
    }

    private static ContentItem CreateBagContact(string contentType, params ContentItem[] contactMethods)
    {
        var contact = new ContentItem
        {
            ContentItemId = "contact-id",
            ContentType = contentType,
        };

        contact.Alter<OmnichannelContactPart>(_ => { });
        contact.Apply(OmnichannelConstants.NamedParts.ContactMethods, CreateBag(contactMethods));

        return contact;
    }

    private static BagPart CreateBag(params ContentItem[] contactMethods)
    {
        var bagPart = new BagPart();

        foreach (var contactMethod in contactMethods)
        {
            bagPart.ContentItems.Add(contactMethod);
        }

        return bagPart;
    }

    private static ContentItem CreatePhoneNumber(string phoneNumber, string type, string contentType = OmnichannelConstants.ContentTypes.PhoneNumber)
    {
        var contentItem = new ContentItem { ContentType = contentType };

        contentItem.Alter<PhoneNumberInfoPart>(part =>
        {
            part.Number = new PhoneField { PhoneNumber = phoneNumber };
            part.Type = new TextField { Text = type };
        });

        return contentItem;
    }

    private static ContentItem CreateEmailAddress(string email)
    {
        var contentItem = new ContentItem { ContentType = OmnichannelConstants.ContentTypes.EmailAddress };

        contentItem.Alter<EmailInfoPart>(part => part.Email = new TextField { Text = email });

        return contentItem;
    }
}
