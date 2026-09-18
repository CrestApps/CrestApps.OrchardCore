using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Models;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

/// <summary>
/// Pins how a contact content item reads as a contact.
/// </summary>
/// <remarks>
/// This projection is what the SMS and voice services now see instead of the content item, so a
/// field it fails to carry is not a rendering bug: it is an opt-out that stops being honoured, or a
/// number that stops being reachable.
/// </remarks>
public sealed class ContentItemOmnichannelContactProjectionTests
{
    private static readonly DateTime _optedOutUtc = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Project_WithoutAContentItem_ReturnsNothing()
        => Assert.Null(ContentItemOmnichannelContactProjection.Project(null));

    [Fact]
    public void Project_CarriesTheIdentityAndKind()
    {
        // Arrange
        var contentItem = new ContentItem
        {
            ContentItemId = "contact-1",
            ContentType = "Customer",
            DisplayText = "A Customer",
        };

        // Act
        var contact = ContentItemOmnichannelContactProjection.Project(contentItem);

        // Assert
        Assert.Equal("contact-1", contact.Id);
        Assert.Equal("Customer", contact.DefinitionName);
        Assert.Equal("A Customer", contact.DisplayText);
    }

    [Fact]
    public void Project_CarriesEveryContactPreferenceAndItsStamp()
    {
        // Arrange
        var contentItem = new ContentItem { ContentItemId = "contact-1", ContentType = "Customer" };
        contentItem.Alter<OmnichannelContactPart>(part =>
        {
            part.TimeZoneId = "America/New_York";
            part.SetDoNotCall(true, _optedOutUtc);
            part.SetDoNotSms(true, _optedOutUtc);
            part.SetDoNotEmail(true, _optedOutUtc);
        });

        // Act
        var contact = ContentItemOmnichannelContactProjection.Project(contentItem);

        // Assert: the stamp matters as much as the flag, because it records when the person asked.
        Assert.Equal("America/New_York", contact.TimeZoneId);
        Assert.True(contact.DoNotCall);
        Assert.Equal(_optedOutUtc, contact.DoNotCallUtc);
        Assert.True(contact.DoNotSms);
        Assert.Equal(_optedOutUtc, contact.DoNotSmsUtc);
        Assert.True(contact.DoNotEmail);
        Assert.Equal(_optedOutUtc, contact.DoNotEmailUtc);
    }

    [Fact]
    public void Project_WhenTheContactHasNeverRecordedAPreference_ReadsAsNotOptedOut()
    {
        // Arrange: a contact that has never carried the part at all.
        var contentItem = new ContentItem { ContentItemId = "contact-1", ContentType = "Customer" };

        // Act
        var contact = ContentItemOmnichannelContactProjection.Project(contentItem);

        // Assert
        Assert.False(contact.DoNotCall);
        Assert.False(contact.DoNotSms);
        Assert.False(contact.DoNotEmail);
    }

    [Fact]
    public void Project_CarriesTheName()
    {
        // Arrange
        var contentItem = new ContentItem { ContentItemId = "contact-1", ContentType = "Customer" };
        contentItem.Alter<OmnichannelContactInfoPart>(part =>
        {
            part.FirstName = new TextField { Text = "Alex" };
            part.LastName = new TextField { Text = "Rivera" };
        });

        // Act
        var contact = ContentItemOmnichannelContactProjection.Project(contentItem);

        // Assert
        Assert.Equal("Alex", contact.FirstName);
        Assert.Equal("Rivera", contact.LastName);
    }

    [Fact]
    public void Project_CarriesTheContactMethods()
    {
        // Arrange
        var contentItem = WithContactMethods(
            new ContentItem { ContentItemId = "contact-1", ContentType = "Customer" },
            Email("  person@example.test  "),
            Phone("+15550000001", ContactPhoneNumberType.Home, extension: "22"),
            Phone("+15550000002", ContactPhoneNumberType.Cell));

        // Act
        var contact = ContentItemOmnichannelContactProjection.Project(contentItem);

        // Assert
        Assert.Equal("person@example.test", contact.GetPrimaryEmail());
        Assert.Equal(2, contact.PhoneNumbers.Count);

        var home = contact.PhoneNumbers.Single(number => number.Type == ContactPhoneNumberType.Home);

        Assert.Equal("+15550000001", home.Number);
        Assert.Equal("22", home.Extension);
    }

    [Fact]
    public void Project_PrefersTheMobileNumber()
    {
        // Arrange: the home number comes first in the bag, so ordering alone would pick the wrong one.
        var contentItem = WithContactMethods(
            new ContentItem { ContentItemId = "contact-1", ContentType = "Customer" },
            Phone("+15550000001", ContactPhoneNumberType.Home),
            Phone("+15550000002", ContactPhoneNumberType.Cell));

        // Act
        var contact = ContentItemOmnichannelContactProjection.Project(contentItem);

        // Assert: a text sent to the home number reaches nobody.
        Assert.Equal("+15550000002", contact.GetPrimaryPhoneNumber()?.Number);
    }

    [Fact]
    public void Project_WithOnlyAnUnclassifiedNumber_StillReportsIt()
    {
        // Arrange
        var contentItem = WithContactMethods(
            new ContentItem { ContentItemId = "contact-1", ContentType = "Customer" },
            Phone("+15550000003", type: null));

        // Act
        var contact = ContentItemOmnichannelContactProjection.Project(contentItem);

        // Assert: a contact with one number nobody classified is still reachable.
        Assert.Equal("+15550000003", contact.GetPrimaryPhoneNumber()?.Number);
    }

    [Fact]
    public void Project_SkipsAnEmptyContactMethod()
    {
        // Arrange
        var contentItem = WithContactMethods(
            new ContentItem { ContentItemId = "contact-1", ContentType = "Customer" },
            Email("   "),
            Phone(" ", ContactPhoneNumberType.Cell));

        // Act
        var contact = ContentItemOmnichannelContactProjection.Project(contentItem);

        // Assert: a blank row must not read as a way to reach somebody.
        Assert.Empty(contact.Emails);
        Assert.Empty(contact.PhoneNumbers);
        Assert.Null(contact.GetPrimaryEmail());
        Assert.Null(contact.GetPrimaryPhoneNumber());
    }

    private static ContentItem WithContactMethods(ContentItem contentItem, params ContentItem[] methods)
    {
        var bag = new BagPart();
        bag.ContentItems.AddRange(methods);
        contentItem.Apply(OmnichannelConstants.NamedParts.ContactMethods, bag);

        return contentItem;
    }

    private static ContentItem Email(string email)
    {
        var item = new ContentItem { ContentType = OmnichannelConstants.ContentTypes.EmailAddress };
        item.Alter<EmailInfoPart>(part => part.Email = new TextField { Text = email });

        return item;
    }

    private static ContentItem Phone(string number, string type, string extension = null)
    {
        var item = new ContentItem { ContentType = OmnichannelConstants.ContentTypes.PhoneNumber };
        item.Alter<PhoneNumberInfoPart>(part =>
        {
            part.Number = new PhoneField { PhoneNumber = number };
            part.Extension = new TextField { Text = extension };
            part.Type = new TextField { Text = type };
        });

        return item;
    }
}
