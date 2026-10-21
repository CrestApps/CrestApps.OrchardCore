using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers.Verifications.Services;
using CrestApps.OrchardCore.Tests.Modules.Omnichannel;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Tests.Modules.PhoneNumbers.Verifications;

public sealed class OmnichannelContactPhoneNumberResolverTests
{
    [Fact]
    public void GetPreferredPhoneNumber_SelectsHighestPriorityType()
    {
        // Arrange
        var contact = CreateContact(
            CreatePhoneNumberContentItem("+15553334444", "Home"),
            CreatePhoneNumberContentItem("  +15551112222  ", "Cell"),
            CreatePhoneNumberContentItem("+15555556666", "Work"));

        // Act
        var preferred = OmnichannelContactPhoneNumberResolver.GetPreferredPhoneNumber(TestContactMethods.Accessor, contact);

        // Assert
        Assert.NotNull(preferred);
        Assert.Equal("+15551112222", preferred.PhoneNumber);
        Assert.Equal(ContactPhoneTypes.Cell, preferred.Type);
    }

    [Fact]
    public void GetPreferredPhoneNumber_WhenNoContactMethods_ReturnsNull()
    {
        // Arrange
        var contact = new ContentItem
        {
            ContentType = "Contact",
        };

        // Act
        var preferred = OmnichannelContactPhoneNumberResolver.GetPreferredPhoneNumber(TestContactMethods.Accessor, contact);

        // Assert
        Assert.Null(preferred);
    }

    [Fact]
    public void GetPreferredPhoneNumber_WhenOnlyUnrecognizedTypes_ReturnsNull()
    {
        // Arrange
        var contact = CreateContact(CreatePhoneNumberContentItem("+15553334444", "Fax"));

        // Act
        var preferred = OmnichannelContactPhoneNumberResolver.GetPreferredPhoneNumber(TestContactMethods.Accessor, contact);

        // Assert
        Assert.Null(preferred);
    }

    [Fact]
    public void AlterPhoneNumberItem_ChangesTheItemHoldingTheNumberAndStoresTheBag()
    {
        // Arrange
        var contact = CreateContact(
            CreatePhoneNumberContentItem("+15553334444", "Home"),
            CreatePhoneNumberContentItem("+15551112222", "Cell"));
        var preferred = OmnichannelContactPhoneNumberResolver.GetPreferredPhoneNumber(TestContactMethods.Accessor, contact);

        // Act
        var altered = OmnichannelContactPhoneNumberResolver.AlterPhoneNumberItem(
            TestContactMethods.Accessor,
            contact,
            preferred,
            item =>
            {
                item.DisplayText = "changed";

                return true;
            });

        // Assert
        Assert.True(altered);

        // Read back from the stored JSON, not the cached part, so the change is known to be kept.
        var stored = ((JsonObject)contact.Content)[OmnichannelConstants.NamedParts.ContactMethods]
            .ToObject<BagPart>();
        Assert.Equal(["Home: +15553334444", "changed"], stored.ContentItems.Select(item => item.DisplayText));
    }

    [Fact]
    public void AlterPhoneNumberItem_WhenTheTypeHasAProvider_LeavesItsBagAsItIs()
    {
        // Arrange
        var accessor = TestContactMethods
            .CreateServices(services => services.AddContactMethodsProvider<FieldContactMethodsProvider>("Contact"))
            .GetRequiredService<IContactMethodsAccessor>();
        var contact = CreateContact(CreatePhoneNumberContentItem("+15551112222", "Cell"));

        // Act
        var altered = OmnichannelContactPhoneNumberResolver.AlterPhoneNumberItem(
            accessor,
            contact,
            new ContactPhoneNumber { PhoneNumber = "+15551112222", Type = ContactPhoneTypes.Cell },
            item =>
            {
                item.DisplayText = "changed";

                return true;
            });

        // Assert
        Assert.False(altered);
        Assert.Equal("Cell: +15551112222", Assert.Single(contact.Get<BagPart>(OmnichannelConstants.NamedParts.ContactMethods).ContentItems).DisplayText);
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
