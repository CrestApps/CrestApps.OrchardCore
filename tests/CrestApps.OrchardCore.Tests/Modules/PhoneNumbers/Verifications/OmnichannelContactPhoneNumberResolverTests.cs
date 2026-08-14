using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Tests.Modules.PhoneNumbers.Verifications;

public sealed class OmnichannelContactPhoneNumberResolverTests
{
    [Fact]
    public void GetPreferredPhoneNumberContentItem_SelectsHighestPriorityType()
    {
        // Arrange
        var contact = CreateContact(
            CreatePhoneNumberContentItem("+15553334444", "Home"),
            CreatePhoneNumberContentItem("+15551112222", "Cell"),
            CreatePhoneNumberContentItem("+15555556666", "Work"));

        // Act
        var preferred = OmnichannelContactPhoneNumberResolver.GetPreferredPhoneNumberContentItem(contact);

        // Assert
        Assert.NotNull(preferred);
        Assert.Equal("+15551112222", OmnichannelContactPhoneNumberResolver.GetPhoneNumber(preferred));
    }

    [Fact]
    public void GetPreferredPhoneNumberContentItem_WhenNoContactMethods_ReturnsNull()
    {
        // Arrange
        var contact = new ContentItem
        {
            ContentType = "Contact",
        };

        // Act
        var preferred = OmnichannelContactPhoneNumberResolver.GetPreferredPhoneNumberContentItem(contact);

        // Assert
        Assert.Null(preferred);
    }

    [Fact]
    public void GetPreferredPhoneNumberContentItem_WhenOnlyUnrecognizedTypes_ReturnsNull()
    {
        // Arrange
        var contact = CreateContact(CreatePhoneNumberContentItem("+15553334444", "Fax"));

        // Act
        var preferred = OmnichannelContactPhoneNumberResolver.GetPreferredPhoneNumberContentItem(contact);

        // Assert
        Assert.Null(preferred);
    }

    [Fact]
    public void GetPhoneNumber_TrimsStoredNumber()
    {
        // Arrange
        var phoneNumberContentItem = CreatePhoneNumberContentItem("  +15551112222  ", "Cell");

        // Act
        var phoneNumber = OmnichannelContactPhoneNumberResolver.GetPhoneNumber(phoneNumberContentItem);

        // Assert
        Assert.Equal("+15551112222", phoneNumber);
    }

    [Fact]
    public void GetPhoneNumber_WhenContentItemIsNull_ReturnsNull()
    {
        // Act
        var phoneNumber = OmnichannelContactPhoneNumberResolver.GetPhoneNumber(null);

        // Assert
        Assert.Null(phoneNumber);
    }

    private static ContentItem CreateContact(params ContentItem[] phoneNumbers)
    {
        var contact = new ContentItem
        {
            ContentType = "Contact",
        };

        var bagPart = new BagPart();

        foreach (var phoneNumber in phoneNumbers)
        {
            bagPart.ContentItems.Add(phoneNumber);
        }

        contact.Apply(OmnichannelConstants.NamedParts.ContactMethods, bagPart);

        return contact;
    }

    private static ContentItem CreatePhoneNumberContentItem(string number, string type)
    {
        var contentItem = new ContentItem
        {
            ContentType = OmnichannelConstants.ContentTypes.PhoneNumber,
            DisplayText = $"{type}: {number}",
        };

        contentItem.Alter<PhoneNumberInfoPart>(part =>
        {
            part.Number = new PhoneField { PhoneNumber = number };
            part.Type = new TextField { Text = type };
        });

        return contentItem;
    }
}
