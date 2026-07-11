using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Indexes;

public sealed class OmnichannelContactPhoneIndexProviderTests
{
    [Fact]
    public void CreateIndex_WhenContactHasPrimaryCellAndHome_IndexesBothFormats()
    {
        // Arrange
        var provider = new OmnichannelContactPhoneIndexProvider(new DefaultPhoneNumberService());
        var contact = CreateContact(
            published: true,
            latest: false,
            CreatePhoneNumber("+17024993350", "7024993350", "US", "Cell"),
            CreatePhoneNumber("+12125550123", "2125550123", "US", "Home"));

        // Act
        var index = provider.CreateIndex(contact);

        // Assert
        Assert.NotNull(index);
        Assert.Equal("contact-id", index.ContentItemId);
        Assert.True(index.Published);
        Assert.False(index.Latest);
        Assert.Equal("+17024993350", index.E164PrimaryCellPhoneNumber);
        Assert.Equal("7024993350", index.NationalPrimaryCellPhoneNumber);
        Assert.Equal("+12125550123", index.E164PrimaryHomePhoneNumber);
        Assert.Equal("2125550123", index.NationalPrimaryHomePhoneNumber);
    }

    [Fact]
    public void CreateIndex_WhenNationalNumberIsMissing_DerivesItFromE164()
    {
        // Arrange
        var provider = new OmnichannelContactPhoneIndexProvider(new DefaultPhoneNumberService());
        var contact = CreateContact(
            published: false,
            latest: true,
            CreatePhoneNumber("+17024993350", null, "US", "Cell"));

        // Act
        var index = provider.CreateIndex(contact);

        // Assert
        Assert.NotNull(index);
        Assert.False(index.Published);
        Assert.True(index.Latest);
        Assert.Equal("7024993350", index.NationalPrimaryCellPhoneNumber);
    }

    [Fact]
    public void CreateIndex_WhenRegionCannotBeDetermined_PreservesAllDigits()
    {
        // Arrange
        var provider = new OmnichannelContactPhoneIndexProvider(new DefaultPhoneNumberService());
        var contact = CreateContact(
            published: false,
            latest: true,
            CreatePhoneNumber("+999123456", null, null, "Cell"));

        // Act
        var index = provider.CreateIndex(contact);

        // Assert
        Assert.NotNull(index);
        Assert.Equal("999123456", index.NationalPrimaryCellPhoneNumber);
    }

    [Fact]
    public void CreateIndex_WhenContentItemIsNeitherPublishedNorLatest_ReturnsNull()
    {
        // Arrange
        var provider = new OmnichannelContactPhoneIndexProvider(new DefaultPhoneNumberService());
        var contact = CreateContact(
            published: false,
            latest: false,
            CreatePhoneNumber("+17024993350", "7024993350", "US", "Cell"));

        // Act
        var index = provider.CreateIndex(contact);

        // Assert
        Assert.Null(index);
    }

    private static ContentItem CreateContact(
        bool published,
        bool latest,
        params ContentItem[] phoneNumbers)
    {
        var contact = new ContentItem
        {
            ContentItemId = "contact-id",
            ContentType = "Contact",
            Published = published,
            Latest = latest,
        };

        contact.Alter<OmnichannelContactPart>(_ => { });

        var bagPart = new BagPart();

        foreach (var phoneNumber in phoneNumbers)
        {
            bagPart.ContentItems.Add(phoneNumber);
        }

        contact.Apply(OmnichannelConstants.NamedParts.ContactMethods, bagPart);

        return contact;
    }

    private static ContentItem CreatePhoneNumber(
        string e164Number,
        string nationalNumber,
        string countryCode,
        string type)
    {
        var contentItem = new ContentItem
        {
            ContentType = OmnichannelConstants.ContentTypes.PhoneNumber,
        };

        contentItem.Alter<PhoneNumberInfoPart>(part =>
        {
            part.Number = new PhoneField
            {
                PhoneNumber = e164Number,
                NationalNumber = nationalNumber,
                CountryCode = countryCode,
            };
            part.Type = new TextField
            {
                Text = type,
            };
        });

        return contentItem;
    }
}
