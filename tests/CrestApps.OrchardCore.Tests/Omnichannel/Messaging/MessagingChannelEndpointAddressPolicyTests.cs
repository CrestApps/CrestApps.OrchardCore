using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using Microsoft.Extensions.Localization;
using Moq;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// Inbound traffic finds its endpoint by exact address after the channel has normalized it, so a new channel's endpoint
/// saved as typed would never match its own inbound. The workspace's address policy makes the channel-endpoint handler
/// normalize and validate every messaging channel's endpoints, so a new channel does not have to.
/// </summary>
public sealed class MessagingChannelEndpointAddressPolicyTests
{
    [Fact]
    public void NewChannelEndpoint_IsStoredInItsChannelsNormalizedForm()
    {
        var policy = CreatePolicy();

        Assert.True(policy.AppliesTo("Chat"));
        Assert.Equal("support@example.com", policy.Normalize("Chat", "  Support@Example.COM "));
    }

    [Fact]
    public void NewChannelEndpoint_WithAnAddressTheChannelCannotUse_IsRefused()
    {
        var policy = CreatePolicy();

        Assert.NotNull(policy.Validate("Chat", "not an address"));
        Assert.Null(policy.Validate("Chat", "ann@example.com"));
    }

    [Fact]
    public void ChannelsNoFeatureServes_AndChannelsTheOmnichannelHandlerOwns_AreLeftToIt()
    {
        var policy = CreatePolicy(alsoRegisterSms: true);

        Assert.False(policy.AppliesTo("Fax"));
        Assert.False(policy.AppliesTo("SMS"));
        Assert.False(policy.AppliesTo(null));
    }

    private static MessagingChannelEndpointAddressPolicy CreatePolicy(bool alsoRegisterSms = false)
    {
        var chat = new Mock<IMessagingChannel>();
        chat.SetupGet(c => c.Name).Returns("Chat");
        chat.SetupGet(c => c.DisplayName).Returns(new LocalizedString("Chat", "Chat"));
        chat.Setup(c => c.NormalizeAddress(It.IsAny<string>())).Returns((string address) => address?.Trim().ToLowerInvariant());
        chat.Setup(c => c.IsValidAddress(It.IsAny<string>())).Returns((string address) => address.Contains('@'));

        var channels = alsoRegisterSms
            ? MessagingTestChannels.Resolver(chat.Object, MessagingTestChannels.Sms(MessagingTestChannels.AcceptingDispatcher().Object))
            : MessagingTestChannels.Resolver(chat.Object);

        return new MessagingChannelEndpointAddressPolicy(channels, new PassThroughLocalizer());
    }

    private sealed class PassThroughLocalizer : IStringLocalizer<MessagingChannelEndpointAddressPolicy>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
