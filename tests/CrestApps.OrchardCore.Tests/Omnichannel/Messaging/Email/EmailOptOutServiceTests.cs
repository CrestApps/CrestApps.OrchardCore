using CrestApps.OrchardCore.Consent;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email;

public sealed class EmailOptOutServiceTests
{
    [Fact]
    public async Task OptOutAsync_AnAddressNoContactHolds_IsStillOptedOut()
    {
        // Arrange
        var consent = new Mock<IConsentService>();
        var service = CreateService(consent, contactIds: []);

        // Act
        var marked = await service.OptOutAsync("Jane@Example.com", TestContext.Current.CancellationToken);

        // Assert: the address itself is opted out, so the next broadcast to it is refused.
        Assert.Equal(0, marked);
        consent.Verify(value => value.RecordAsync(
            ConsentChannels.Email,
            "jane@example.com",
            It.Is<ConsentChange>(change => change.OptedOut && change.ContactId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OptOutAsync_AContactHoldingTheAddress_IsMarkedThroughTheConsentService()
    {
        // Arrange
        var consent = new Mock<IConsentService>();
        var service = CreateService(consent, contactIds: ["contact-1"]);

        // Act
        var marked = await service.OptOutAsync("ann@example.com", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, marked);
        consent.Verify(value => value.RecordAsync(ConsentChannels.Email, "ann@example.com", It.Is<ConsentChange>(change => change.OptedOut && change.ContactId == null), It.IsAny<CancellationToken>()), Times.Once);
        consent.Verify(value => value.RecordAsync(ConsentChannels.Email, "ann@example.com", It.Is<ConsentChange>(change => change.OptedOut && change.ContactId == "contact-1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static EmailOptOutService CreateService(Mock<IConsentService> consent, IReadOnlyList<string> contactIds)
    {
        var channel = new Mock<IMessagingChannel>();
        channel.Setup(value => value.FindContactIdsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(contactIds);

        var resolver = new Mock<IMessagingChannelResolver>();
        resolver.Setup(value => value.Get(OmnichannelConstants.Channels.Email)).Returns(channel.Object);

        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(value => value.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync((string id, VersionOptions _) =>
            {
                var contact = new ContentItem { ContentItemId = id, ContentType = "Contact" };
                contact.Weld(new OmnichannelContactPart());

                return contact;
            });

        return new EmailOptOutService(resolver.Object, contentManager.Object, consent.Object, NullLogger<EmailOptOutService>.Instance);
    }
}
