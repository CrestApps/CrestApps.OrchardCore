using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Indexes;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Indexes;

public sealed class OmnichannelContactIndexProviderTests
{
    [Fact]
    public void CreateIndex_WhenContactIsPublished_IndexesVersionAndPrimaryContactDetails()
    {
        // Arrange
        var provider = new OmnichannelContactIndexProvider(new DefaultPhoneNumberService());
        var contact = CreateContact(
            published: true,
            latest: false,
            " America/Los_Angeles ",
            CreateEmailAddress("lead@example.com"),
            CreatePhoneNumber("(702) 499-3350", "702 499 3350", "US", "cell"),
            CreatePhoneNumber("+15551112222", "5551112222", "US", "Cell"),
            CreatePhoneNumber("+12125550123", null, null, "HOME"),
            CreatePhoneNumber("+13105550123", "3105550123", "US", "Work"));

        // Act
        var index = provider.CreateIndex(contact);

        // Assert
        Assert.NotNull(index);
        Assert.Equal("contact-id", index.ContentItemId);
        Assert.True(index.Published);
        Assert.False(index.Latest);
        Assert.Equal("America/Los_Angeles", index.TimeZoneId);
        Assert.Equal("lead@example.com", index.PrimaryEmailAddress);
        Assert.Equal("7024993350", index.PrimaryCellPhoneNumber);
        Assert.Equal("+17024993350", index.NormalizedPrimaryCellPhoneNumber);
        Assert.Equal("2125550123", index.PrimaryHomePhoneNumber);
        Assert.Equal("+12125550123", index.NormalizedPrimaryHomePhoneNumber);
    }

    [Fact]
    public void CreateIndex_WhenContactIsLatestDraft_IndexesLatestVersion()
    {
        // Arrange
        var provider = new OmnichannelContactIndexProvider(new DefaultPhoneNumberService());
        var contact = CreateContact(
            published: false,
            latest: true,
            timeZoneId: null,
            CreatePhoneNumber("+17024993350", null, "US", "Cell"));

        // Act
        var index = provider.CreateIndex(contact);

        // Assert
        Assert.NotNull(index);
        Assert.False(index.Published);
        Assert.True(index.Latest);
        Assert.Equal("+17024993350", index.NormalizedPrimaryCellPhoneNumber);
        Assert.Equal("7024993350", index.PrimaryCellPhoneNumber);
    }

    [Fact]
    public void CreateIndex_WhenRegionIsNullAndNumberIsNational_PreservesNationalDigits()
    {
        // Arrange
        var provider = new OmnichannelContactIndexProvider(new DefaultPhoneNumberService());
        var contact = CreateContact(
            published: false,
            latest: true,
            timeZoneId: null,
            CreatePhoneNumber("(702) 499-3350", null, null, "Cell"));

        // Act
        var index = provider.CreateIndex(contact);

        // Assert
        Assert.NotNull(index);
        Assert.Null(index.NormalizedPrimaryCellPhoneNumber);
        Assert.Equal("7024993350", index.PrimaryCellPhoneNumber);
    }

    [Fact]
    public void CreateIndex_WhenRegionIsUnknown_PreservesAllDigitsAndExplicitE164()
    {
        // Arrange
        var provider = new OmnichannelContactIndexProvider(new DefaultPhoneNumberService());
        var contact = CreateContact(
            published: false,
            latest: true,
            timeZoneId: null,
            CreatePhoneNumber("+999123456", null, "XX", "Cell"));

        // Act
        var index = provider.CreateIndex(contact);

        // Assert
        Assert.NotNull(index);
        Assert.Equal("+999123456", index.NormalizedPrimaryCellPhoneNumber);
        Assert.Equal("999123456", index.PrimaryCellPhoneNumber);
    }

    [Fact]
    public void CreateIndex_WhenContactHasNoMethods_StillIndexesEligibleVersion()
    {
        // Arrange
        var provider = new OmnichannelContactIndexProvider(new DefaultPhoneNumberService());
        var contact = CreateContact(
            published: true,
            latest: true,
            "UTC");

        // Act
        var index = provider.CreateIndex(contact);

        // Assert
        Assert.NotNull(index);
        Assert.True(index.Published);
        Assert.True(index.Latest);
        Assert.Equal("UTC", index.TimeZoneId);
        Assert.Null(index.PrimaryCellPhoneNumber);
        Assert.Null(index.PrimaryHomePhoneNumber);
    }

    [Fact]
    public void CreateIndex_WhenContentItemIsNeitherPublishedNorLatest_ReturnsNull()
    {
        // Arrange
        var provider = new OmnichannelContactIndexProvider(new DefaultPhoneNumberService());
        var contact = CreateContact(
            published: false,
            latest: false,
            timeZoneId: null,
            CreatePhoneNumber("+17024993350", "7024993350", "US", "Cell"));

        // Act
        var index = provider.CreateIndex(contact);

        // Assert
        Assert.Null(index);
    }

    private static ContentItem CreateContact(
        bool published,
        bool latest,
        string timeZoneId,
        params ContentItem[] contactMethods)
    {
        var contact = new ContentItem
        {
            ContentItemId = "contact-id",
            ContentType = "Contact",
            Published = published,
            Latest = latest,
        };

        contact.Alter<OmnichannelContactPart>(part => part.TimeZoneId = timeZoneId);

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

    private static ContentItem CreatePhoneNumber(
        string phoneNumber,
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
                PhoneNumber = phoneNumber,
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
