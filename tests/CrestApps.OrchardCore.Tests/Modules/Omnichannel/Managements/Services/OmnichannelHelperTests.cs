using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Services;

/// <summary>
/// Proves the preferred-destination resolver honors a contact's do-not-contact flags per channel and, for a phone
/// call, chooses the reachable number in a fixed preference order (cell before home before office before work
/// before other), while SMS reaches only a cell number. These are the routing decisions an outbound campaign
/// depends on, so a mis-ordering or an ignored suppression flag would contact a customer on the wrong number or a
/// number they opted out of.
/// </summary>
public sealed class OmnichannelHelperTests
{
    [Fact]
    public void GetPreferredDestenation_WhenContactIsDoNotCall_ReturnsNullForPhone()
    {
        // Arrange
        var contact = CreateContact(true, false, false, CreatePhoneNumber("+15550001111", "Cell"));

        // Act
        var destination = OmnichannelHelper.GetPreferredDestenation(contact, OmnichannelConstants.Channels.Phone);

        // Assert - a suppressed contact is never dialed even though a reachable number exists.
        Assert.Null(destination);
    }

    [Fact]
    public void GetPreferredDestenation_WhenContactIsDoNotSms_ReturnsNullForSms()
    {
        // Arrange
        var contact = CreateContact(false, true, false, CreatePhoneNumber("+15550001111", "Cell"));

        // Act
        var destination = OmnichannelHelper.GetPreferredDestenation(contact, OmnichannelConstants.Channels.Sms);

        // Assert
        Assert.Null(destination);
    }

    [Fact]
    public void GetPreferredDestenation_WhenContactIsDoNotEmail_ReturnsNullForEmail()
    {
        // Arrange
        var contact = CreateContact(false, false, true, CreateEmail("lead@example.com"));

        // Act
        var destination = OmnichannelHelper.GetPreferredDestenation(contact, OmnichannelConstants.Channels.Email);

        // Assert
        Assert.Null(destination);
    }

    [Fact]
    public void GetPreferredDestenation_ForPhone_PrefersTheCellNumber()
    {
        // Arrange - the office and home numbers are listed first, but the cell number outranks them.
        var contact = CreateContact(
            CreatePhoneNumber("+15550000003", "Office"),
            CreatePhoneNumber("+15550000002", "Home"),
            CreatePhoneNumber("+15550000001", "Cell"));

        // Act
        var destination = OmnichannelHelper.GetPreferredDestenation(contact, OmnichannelConstants.Channels.Phone);

        // Assert
        Assert.Equal("+15550000001", destination);
    }

    [Fact]
    public void GetPreferredDestenation_ForPhone_FallsBackToTheHigherPriorityAvailableNumber()
    {
        // Arrange - no cell number, so home (priority 2) wins over office (priority 3).
        var contact = CreateContact(
            CreatePhoneNumber("+15550000003", "Office"),
            CreatePhoneNumber("+15550000002", "Home"));

        // Act
        var destination = OmnichannelHelper.GetPreferredDestenation(contact, OmnichannelConstants.Channels.Phone);

        // Assert
        Assert.Equal("+15550000002", destination);
    }

    [Fact]
    public void GetPreferredDestenation_ForPhone_IgnoresUnrecognizedNumberTypes()
    {
        // Arrange - a number whose type is not one of the known ranks is not a routable destination.
        var contact = CreateContact(CreatePhoneNumber("+15550000009", "Fax"));

        // Act
        var destination = OmnichannelHelper.GetPreferredDestenation(contact, OmnichannelConstants.Channels.Phone);

        // Assert
        Assert.Null(destination);
    }

    [Fact]
    public void GetPreferredDestenation_ForSms_ReachesOnlyACellNumber()
    {
        // Arrange - SMS is only delivered to a cell number, so a home number is not used.
        var contact = CreateContact(
            CreatePhoneNumber("+15550000002", "Home"),
            CreatePhoneNumber("+15550000001", "Cell"));

        // Act
        var destination = OmnichannelHelper.GetPreferredDestenation(contact, OmnichannelConstants.Channels.Sms);

        // Assert
        Assert.Equal("+15550000001", destination);
    }

    [Fact]
    public void GetPreferredDestenation_ForSms_WhenOnlyANonCellNumberExists_ReturnsNull()
    {
        // Arrange
        var contact = CreateContact(CreatePhoneNumber("+15550000002", "Home"));

        // Act
        var destination = OmnichannelHelper.GetPreferredDestenation(contact, OmnichannelConstants.Channels.Sms);

        // Assert
        Assert.Null(destination);
    }

    [Fact]
    public void GetPreferredDestenation_ForEmail_ReturnsTheFirstEmailAddress()
    {
        // Arrange
        var contact = CreateContact(
            CreateEmail("first@example.com"),
            CreateEmail("second@example.com"));

        // Act
        var destination = OmnichannelHelper.GetPreferredDestenation(contact, OmnichannelConstants.Channels.Email);

        // Assert
        Assert.Equal("first@example.com", destination);
    }

    [Fact]
    public void GetPreferredDestenation_WhenNoContactMethodsExist_ReturnsNull()
    {
        // Arrange
        var contact = CreateContact(false, false, false);

        // Act
        var destination = OmnichannelHelper.GetPreferredDestenation(contact, OmnichannelConstants.Channels.Phone);

        // Assert
        Assert.Null(destination);
    }

    private static ContentItem CreateContact(params ContentItem[] contactMethods)
        => CreateContact(doNotCall: false, doNotSms: false, doNotEmail: false, contactMethods);

    private static ContentItem CreateContact(bool doNotCall = false, bool doNotSms = false, bool doNotEmail = false, params ContentItem[] contactMethods)
    {
        var contact = new ContentItem
        {
            ContentItemId = "contact-id",
            ContentType = "Contact",
        };

        contact.Alter<OmnichannelContactPart>(part =>
        {
            part.DoNotCall = doNotCall;
            part.DoNotSms = doNotSms;
            part.DoNotEmail = doNotEmail;
        });

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
            part.Number = new PhoneField { PhoneNumber = phoneNumber };
            part.Type = new TextField { Text = type };
        });

        return contentItem;
    }

    private static ContentItem CreateEmail(string email)
    {
        var contentItem = new ContentItem
        {
            ContentType = OmnichannelConstants.ContentTypes.EmailAddress,
        };

        contentItem.Alter<EmailInfoPart>(part => part.Email = new TextField { Text = email });

        return contentItem;
    }
}
