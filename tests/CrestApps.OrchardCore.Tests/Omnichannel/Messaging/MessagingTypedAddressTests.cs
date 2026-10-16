using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Sms.Services;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// An address typed in the composer for someone who is not in the contacts is sent exactly as written, so it must
/// already be in the form the channel needs: on SMS, E.164. A contact's stored address is held only to the channel's
/// looser rule, so contacts saved in a national format stay reachable.
/// </summary>
public sealed class MessagingTypedAddressTests
{
    [Theory]
    [InlineData("+12015550123", true)]
    [InlineData("+442071838750", true)]
    [InlineData("2015550123", false)]
    [InlineData("201-555-0123", false)]
    [InlineData("+1 201 555 0123", false)]
    [InlineData("+1 (201) 555-0123", false)]
    [InlineData("123-4567", false)]
    [InlineData("+1201555012", false)]
    [InlineData("+120155501234567", false)]
    [InlineData("+1201555O123", false)]
    [InlineData("ann@example.com", false)]
    [InlineData("+", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void SmsChannel_AcceptsATypedNumberOnlyInE164(string address, bool valid)
    {
        var channel = MessagingTestChannels.Sms(Mock.Of<ISmsDispatcher>());

        Assert.Equal(valid, channel.IsValidNewAddress(address));
    }

    [Theory]
    [InlineData("2015550123")]
    [InlineData("12015550123")]
    public void SmsChannel_StillAcceptsANationalNumber_ForAStoredContact(string address)
    {
        var channel = MessagingTestChannels.Sms(Mock.Of<ISmsDispatcher>());

        Assert.True(channel.IsValidAddress(address));
        Assert.False(channel.IsValidNewAddress(address));
    }

    [Fact]
    public void GetAddress_OffersAnE164Number_AsTyped()
    {
        var search = Search(MessagingTestChannels.Sms(Mock.Of<ISmsDispatcher>()));

        var address = search.GetAddress(" +12015550123 ", "SMS");

        Assert.NotNull(address);
        Assert.Null(address.Id);
        Assert.Equal("+12015550123", address.Address);
    }

    [Theory]
    [InlineData("2015550123")]
    [InlineData("201-555-0123")]
    [InlineData("123-4567")]
    [InlineData("Ann")]
    public void GetAddress_NeverReshapesANumberIntoE164(string term)
    {
        var search = Search(MessagingTestChannels.Sms(Mock.Of<ISmsDispatcher>()));

        Assert.Null(search.GetAddress(term, "SMS"));
    }

    [Fact]
    public async Task FindInvalidNewAddresses_RefusesANationalNumberNoContactOwns()
    {
        var channel = FakeChannel(contactAddresses: []);
        var search = Search(channel.Object);

        var invalid = await search.FindInvalidNewAddressesAsync(["+12015550123", "2015550199", "201-555-0188"], "SMS", CancellationToken.None);

        Assert.Equal(["2015550199", "201-555-0188"], invalid);
    }

    [Fact]
    public async Task FindInvalidNewAddresses_AcceptsAContactsStoredNationalNumber()
    {
        var channel = FakeChannel(contactAddresses: ["2015550199"]);
        var search = Search(channel.Object);

        var invalid = await search.FindInvalidNewAddressesAsync(["2015550199"], "SMS", CancellationToken.None);

        Assert.Empty(invalid);
    }

    // A phone channel whose typed-address rule is the SMS one, and whose contacts own the specified addresses.
    private static Mock<IMessagingChannel> FakeChannel(string[] contactAddresses)
    {
        var sms = MessagingTestChannels.Sms(Mock.Of<ISmsDispatcher>());
        var channel = new Mock<IMessagingChannel>();

        channel.SetupGet(c => c.Name).Returns("SMS");
        channel.SetupGet(c => c.DisplayName).Returns(new LocalizedString("SMS", "SMS"));
        channel.SetupGet(c => c.Capabilities).Returns(sms.Capabilities);
        channel.Setup(c => c.NormalizeAddress(It.IsAny<string>())).Returns((string address) => sms.NormalizeAddress(address));
        channel.Setup(c => c.IsValidAddress(It.IsAny<string>())).Returns((string address) => sms.IsValidAddress(address));
        channel.Setup(c => c.IsValidNewAddress(It.IsAny<string>())).Returns((string address) => sms.IsValidNewAddress(address));
        channel
            .Setup(c => c.FindContactIdsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string address, CancellationToken _) =>
                contactAddresses.Any(owned => sms.NormalizeAddress(owned) == address) ? ["contact-1"] : []);

        return channel;
    }

    private static MessagingContactSearch Search(IMessagingChannel channel)
        => new(new MessagingChannelResolver([channel]), Mock.Of<IOmnichannelContactTypeProvider>(), Mock.Of<IContentManager>(), Mock.Of<ISession>());
}
