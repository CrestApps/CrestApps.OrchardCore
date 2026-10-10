using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Moq;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.Flows.Models;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email;

public sealed class EmailMessagingChannelTests
{
    [Fact]
    public void Capabilities_DescribeAnEmail()
    {
        // Arrange
        var channel = CreateChannel(Mock.Of<IEmailDispatcher>());

        // Act
        var capabilities = channel.Capabilities;

        // Assert
        Assert.Equal(OmnichannelConstants.Channels.Email, channel.Name);
        Assert.True(capabilities.SupportsSubject);
        Assert.True(capabilities.SupportsMedia);
        Assert.False(capabilities.Attachments.DeliveredAsLinks);
        Assert.False(capabilities.Attachments.ImagesOnly);
        Assert.False(capabilities.ObservesQuietHours);
        Assert.True(capabilities.SupportsBroadcast);
        Assert.Null(capabilities.MaxBodyLength);
    }

    [Fact]
    public void ChannelName_IsTheOmnichannelEmailChannel()
    {
        // The channel spells its name out (see EmailChannelConstants.ChannelName); this keeps it the shared value.
        Assert.Equal(OmnichannelConstants.Channels.Email, EmailChannelConstants.ChannelName);
        Assert.Equal(OmnichannelConstants.Channels.Email, new global::CrestApps.OrchardCore.Omnichannel.Email.Services.EmailAutomatedMessagingChannel().Channel);
    }

    [Fact]
    public void NormalizeAddress_IsTheConversationKeyForEverySpelling()
    {
        // Arrange
        var channel = CreateChannel(Mock.Of<IEmailDispatcher>());

        // Act & Assert
        Assert.Equal(channel.NormalizeAddress("Ann@Example.com"), channel.NormalizeAddress("\"Ann\" <ann@example.com>"));
    }

    [Fact]
    public async Task SendAsync_HandsTheMessageToTheDispatcher()
    {
        // Arrange
        var dispatcher = new Mock<IEmailDispatcher>();
        dispatcher
            .Setup(value => value.SendAsync(It.IsAny<MessagingOutboundMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MessageDispatchResult.Success("m-1"));

        var channel = CreateChannel(dispatcher.Object);
        var message = new MessagingOutboundMessage { ServiceAddress = "support@contoso.com", ContactAddress = "ann@example.com", Body = "Hi" };

        // Act
        var result = await channel.SendAsync(message, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("m-1", result.ProviderMessageId);
        dispatcher.Verify(value => value.SendAsync(message, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void IsOptedOut_ReadsTheDoNotEmailFlag()
    {
        // Arrange
        var channel = CreateChannel(Mock.Of<IEmailDispatcher>());
        var contact = new ContentItem { ContentType = "Contact" };

        contact.Alter<OmnichannelContactPart>(part => part.DoNotEmail = true);

        // Act & Assert
        Assert.True(channel.IsOptedOut(contact));
        Assert.False(channel.IsOptedOut(new ContentItem { ContentType = "Contact" }));
    }

    [Fact]
    public void GetContactAddresses_ListsTheValidEmailsOnTheContactOnceEach()
    {
        // Arrange
        var channel = CreateChannel(Mock.Of<IEmailDispatcher>());
        var contact = new ContentItem { ContentType = "Contact" };

        contact.Alter<BagPart>(OmnichannelConstants.NamedParts.ContactMethods, bag =>
        {
            bag.ContentItems = [Email("Ann@Example.com"), Email("not an email"), Email("ann@example.com"), Email("ann.work@contoso.com")];
        });

        // Act
        var addresses = channel.GetContactAddresses(contact);

        // Assert
        Assert.Equal(["ann@example.com", "ann.work@contoso.com"], addresses);
    }

    private static ContentItem Email(string address)
    {
        var method = new ContentItem { ContentType = OmnichannelConstants.ContentTypes.EmailAddress };

        method.Alter<EmailInfoPart>(part => part.Email = new TextField { Text = address });

        return method;
    }

    private static EmailMessagingChannel CreateChannel(IEmailDispatcher dispatcher)
        => new(
            new Lazy<IEmailDispatcher>(() => dispatcher),
            Mock.Of<IOmnichannelContactTypeProvider>(),
            Mock.Of<ISession>(),
            new PassThroughStringLocalizer<EmailMessagingChannel>());
}
