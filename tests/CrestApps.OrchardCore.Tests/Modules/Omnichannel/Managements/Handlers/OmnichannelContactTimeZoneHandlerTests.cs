using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Handlers;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using Moq;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Builders;
using OrchardCore.Flows.Models;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Handlers;

public sealed class OmnichannelContactTimeZoneHandlerTests
{
    [Fact]
    public async Task CreatingAsync_WhenTimeZoneIsEmpty_DerivesItFromPhoneNumber()
    {
        var handler = CreateHandler(autoDetectTimeZone: true);
        var contact = CreateContact(timeZoneId: null, CreatePhoneNumber("+17024993350", "US", "Cell"));

        await handler.CreatingAsync(new CreateContentContext(contact));

        Assert.True(contact.TryGet<OmnichannelContactPart>(out var part));
        Assert.Equal("America/Los_Angeles", part.TimeZoneId);
    }

    [Fact]
    public async Task UpdatingAsync_WhenNumberIsNationalWithCountryCode_NormalizesAndDerives()
    {
        var handler = CreateHandler(autoDetectTimeZone: true);
        var contact = CreateContact(timeZoneId: null, CreatePhoneNumber("(702) 499-3350", "US", "Cell"));

        await handler.UpdatingAsync(new UpdateContentContext(contact));

        Assert.True(contact.TryGet<OmnichannelContactPart>(out var part));
        Assert.Equal("America/Los_Angeles", part.TimeZoneId);
    }

    [Fact]
    public async Task CreatingAsync_WhenAutoDetectIsDisabled_LeavesTimeZoneEmpty()
    {
        var handler = CreateHandler(autoDetectTimeZone: false);
        var contact = CreateContact(timeZoneId: null, CreatePhoneNumber("+17024993350", "US", "Cell"));

        await handler.CreatingAsync(new CreateContentContext(contact));

        Assert.True(contact.TryGet<OmnichannelContactPart>(out var part));
        Assert.True(string.IsNullOrEmpty(part.TimeZoneId));
    }

    [Fact]
    public async Task CreatingAsync_WhenTimeZoneIsAlreadySet_DoesNotOverrideIt()
    {
        var handler = CreateHandler(autoDetectTimeZone: true);
        var contact = CreateContact(timeZoneId: "America/New_York", CreatePhoneNumber("+17024993350", "US", "Cell"));

        await handler.CreatingAsync(new CreateContentContext(contact));

        Assert.True(contact.TryGet<OmnichannelContactPart>(out var part));
        Assert.Equal("America/New_York", part.TimeZoneId);
    }

    [Fact]
    public async Task CreatingAsync_WhenNoPhoneNumberIsAvailable_LeavesTimeZoneEmpty()
    {
        var handler = CreateHandler(autoDetectTimeZone: true);
        var contact = CreateContact(timeZoneId: null, CreateEmailAddress("lead@example.com"));

        await handler.CreatingAsync(new CreateContentContext(contact));

        Assert.True(contact.TryGet<OmnichannelContactPart>(out var part));
        Assert.True(string.IsNullOrEmpty(part.TimeZoneId));
    }

    [Fact]
    public async Task CreatingAsync_WhenContactPartIsMissing_DoesNothing()
    {
        var handler = CreateHandler(autoDetectTimeZone: true);
        var contact = new ContentItem
        {
            ContentType = "Contact",
        };

        await handler.CreatingAsync(new CreateContentContext(contact));

        Assert.False(contact.TryGet<OmnichannelContactPart>(out _));
    }

    private static OmnichannelContactTimeZoneHandler CreateHandler(bool autoDetectTimeZone)
    {
        var typeDefinition = new ContentTypeDefinitionBuilder()
            .WithName("Contact")
            .WithPart(nameof(OmnichannelContactPart), partBuilder =>
                partBuilder.WithSettings(new OmnichannelContactPartSettings
                {
                    AutoDetectTimeZone = autoDetectTimeZone,
                }))
            .Build();

        var contentDefinitionManager = new Mock<IContentDefinitionManager>();
        contentDefinitionManager
            .Setup(manager => manager.GetTypeDefinitionAsync("Contact"))
            .ReturnsAsync(typeDefinition);

        return new OmnichannelContactTimeZoneHandler(new DefaultPhoneNumberService(), contentDefinitionManager.Object);
    }

    private static ContentItem CreateContact(string timeZoneId, params ContentItem[] contactMethods)
    {
        var contact = new ContentItem
        {
            ContentItemId = "contact-id",
            ContentType = "Contact",
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

    private static ContentItem CreatePhoneNumber(string phoneNumber, string countryCode, string type)
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
