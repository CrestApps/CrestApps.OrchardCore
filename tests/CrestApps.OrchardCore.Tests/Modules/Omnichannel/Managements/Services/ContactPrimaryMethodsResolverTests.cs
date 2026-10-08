using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

/// <summary>
/// The list part's header for a contact shows the contact's primary contact methods under the name: the first email
/// and the first number of each phone type, in the order they appear on the contact.
/// </summary>
public sealed class ContactPrimaryMethodsResolverTests
{
    [Fact]
    public void Resolve_KeepsTheFirstEmailAndTheFirstNumberOfEachPhoneType()
    {
        // Arrange
        var contact = CreateContact(
            CreatePhoneNumber("+17785550100", "Cell"),
            CreateEmailAddress("first@example.com"),
            CreatePhoneNumber("+17025550100", "Home"),
            CreatePhoneNumber("+17785550199", "cell"),
            CreateEmailAddress("second@example.com"),
            CreatePhoneNumber("+13105550100", "Work"));

        // Act
        var methods = ContactPrimaryMethodsResolver.Resolve(contact);

        // Assert
        Assert.Collection(
            methods,
            method => AssertMethod(method, ContactPrimaryMethodKind.Phone, "Cell", "+17785550100"),
            method => AssertMethod(method, ContactPrimaryMethodKind.Email, null, "first@example.com"),
            method => AssertMethod(method, ContactPrimaryMethodKind.Phone, "Home", "+17025550100"),
            method => AssertMethod(method, ContactPrimaryMethodKind.Phone, "Work", "+13105550100"));
    }

    [Fact]
    public void Resolve_OnlyAMobileNumberCanReceiveTextMessages()
    {
        // Arrange
        var contact = CreateContact(
            CreatePhoneNumber("+17785550100", "Cell"),
            CreatePhoneNumber("+17025550100", "Home"),
            CreatePhoneNumber("+13105550100", "Work"),
            CreateEmailAddress("lead@example.com"));

        // Act
        var methods = ContactPrimaryMethodsResolver.Resolve(contact);

        // Assert
        Assert.Equal([true, false, false, false], methods.Select(method => method.CanText));
    }

    [Theory]
    [InlineData("Cell", true)]
    [InlineData(" cell ", true)]
    [InlineData("Mobile", true)]
    [InlineData("Home", false)]
    [InlineData("Work", false)]
    [InlineData("Fax", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsMobileType_OnlyCellAndMobileNumbersCanBeTexted(string phoneType, bool expected)
    {
        // Act
        var canText = ContactPrimaryMethodsResolver.IsMobileType(phoneType);

        // Assert
        Assert.Equal(expected, canText);
    }

    [Fact]
    public void Resolve_SkipsBlankMethodsSoALaterOneOfTheSameKindIsPrimary()
    {
        // Arrange
        var contact = CreateContact(
            CreateEmailAddress("  "),
            CreatePhoneNumber(" ", "Cell"),
            CreateEmailAddress(" lead@example.com "),
            CreatePhoneNumber(" +17785550100 ", "Cell"));

        // Act
        var methods = ContactPrimaryMethodsResolver.Resolve(contact);

        // Assert
        Assert.Collection(
            methods,
            method => AssertMethod(method, ContactPrimaryMethodKind.Email, null, "lead@example.com"),
            method => AssertMethod(method, ContactPrimaryMethodKind.Phone, "Cell", "+17785550100"));
    }

    [Fact]
    public void Resolve_TreatsNumbersWithoutATypeAsOneGroupWithAnEmptyLabel()
    {
        // Arrange
        var contact = CreateContact(
            CreatePhoneNumber("+17785550100", null),
            CreatePhoneNumber("+17785550199", " "));

        // Act
        var methods = ContactPrimaryMethodsResolver.Resolve(contact);

        // Assert
        var method = Assert.Single(methods);
        AssertMethod(method, ContactPrimaryMethodKind.Phone, string.Empty, "+17785550100");
    }

    [Fact]
    public void Resolve_IgnoresBagItemsThatAreNotContactMethods()
    {
        // Arrange
        var contact = CreateContact(new ContentItem { ContentType = "Note" });

        // Act
        var methods = ContactPrimaryMethodsResolver.Resolve(contact);

        // Assert
        Assert.Empty(methods);
    }

    [Fact]
    public void Resolve_WhenTheContactHasNoContactMethods_ReturnsNothing()
    {
        // Arrange
        var contact = CreateContact();

        // Act
        var methods = ContactPrimaryMethodsResolver.Resolve(contact);

        // Assert
        Assert.Empty(methods);
        Assert.Empty(ContactPrimaryMethodsResolver.Resolve(null));
    }

    private static void AssertMethod(ContactPrimaryMethod method, ContactPrimaryMethodKind kind, string label, string value)
    {
        Assert.Equal(kind, method.Kind);
        Assert.Equal(label, method.Label);
        Assert.Equal(value, method.Value);
    }

    private static ContentItem CreateContact(params ContentItem[] contactMethods)
    {
        var contact = new ContentItem
        {
            ContentItemId = "contact-id",
            ContentType = "Contact",
        };

        contact.Alter<OmnichannelContactPart>(_ => { });

        if (contactMethods.Length > 0)
        {
            var bagPart = new BagPart();

            foreach (var contactMethod in contactMethods)
            {
                bagPart.ContentItems.Add(contactMethod);
            }

            contact.Apply(OmnichannelConstants.NamedParts.ContactMethods, bagPart);
        }

        return contact;
    }

    private static ContentItem CreatePhoneNumber(string phoneNumber, string type)
    {
        var contentItem = new ContentItem
        {
            ContentType = OmnichannelConstants.ContentTypes.PhoneNumber,
        };

        contentItem.Alter<PhoneNumberInfoPart>(part =>
        {
            part.Number = new PhoneField
            {
                PhoneNumber = phoneNumber,
            };
            part.Type = new TextField
            {
                Text = type,
            };
        });

        return contentItem;
    }

    private static ContentItem CreateEmailAddress(string email)
    {
        var contentItem = new ContentItem
        {
            ContentType = OmnichannelConstants.ContentTypes.EmailAddress,
        };

        contentItem.Alter<EmailInfoPart>(part =>
        {
            part.Email = new TextField
            {
                Text = email,
            };
        });

        return contentItem;
    }
}
