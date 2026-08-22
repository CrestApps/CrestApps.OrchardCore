using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Core.Omnichannel.Services;

public sealed class OmnichannelChannelEndpointManagerTests
{
    [Theory]
    [InlineData(OmnichannelConstants.Channels.Sms)]
    [InlineData(OmnichannelConstants.Channels.Phone)]
    public async Task GetByServiceAddressAsync_CanonicalizesNumberToE164_BeforeLookup(string channel)
    {
        // The endpoint stores its number as canonical E.164 on save, so an inbound number written in national
        // format must be canonicalized to the same shape before the exact-match store lookup.
        var store = new Mock<IOmnichannelChannelEndpointStore>();
        string capturedAddress = null;
        store
            .Setup(s => s.GetByServiceAddressAsync(channel, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, address, _) => capturedAddress = address)
            .ReturnsAsync(new OmnichannelChannelEndpoint());

        var phoneNumberService = new Mock<IPhoneNumberService>();
        phoneNumberService
            .Setup(p => p.TryFormatToE164("(702) 499-3350", It.IsAny<string>(), out It.Ref<string>.IsAny))
            .Returns((string _, string _, out string e164) =>
            {
                e164 = "+17024993350";
                return true;
            });

        var manager = CreateManager(store.Object, phoneNumberService.Object);

        await manager.GetByServiceAddressAsync(channel, "(702) 499-3350");

        Assert.Equal("+17024993350", capturedAddress);
    }

    [Fact]
    public async Task GetByServiceAddressAsync_LeavesAddressUnchanged_WhenNotCanonicalizable()
    {
        var store = new Mock<IOmnichannelChannelEndpointStore>();
        string capturedAddress = null;
        store
            .Setup(s => s.GetByServiceAddressAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, address, _) => capturedAddress = address)
            .ReturnsAsync((OmnichannelChannelEndpoint)null);

        var phoneNumberService = new Mock<IPhoneNumberService>();
        phoneNumberService
            .Setup(p => p.TryFormatToE164(It.IsAny<string>(), It.IsAny<string>(), out It.Ref<string>.IsAny))
            .Returns((string _, string _, out string e164) =>
            {
                e164 = null;
                return false;
            });

        var manager = CreateManager(store.Object, phoneNumberService.Object);

        await manager.GetByServiceAddressAsync(OmnichannelConstants.Channels.Sms, "not-a-number");

        Assert.Equal("not-a-number", capturedAddress);
    }

    [Fact]
    public async Task GetByServiceAddressAsync_DoesNotCanonicalize_ForNonPhoneChannels()
    {
        var store = new Mock<IOmnichannelChannelEndpointStore>();
        string capturedAddress = null;
        store
            .Setup(s => s.GetByServiceAddressAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, address, _) => capturedAddress = address)
            .ReturnsAsync((OmnichannelChannelEndpoint)null);

        var phoneNumberService = new Mock<IPhoneNumberService>();

        var manager = CreateManager(store.Object, phoneNumberService.Object);

        await manager.GetByServiceAddressAsync("Email", "support@example.com");

        Assert.Equal("support@example.com", capturedAddress);
        phoneNumberService.Verify(
            p => p.TryFormatToE164(It.IsAny<string>(), It.IsAny<string>(), out It.Ref<string>.IsAny),
            Times.Never);
    }

    private static OmnichannelChannelEndpointManager CreateManager(
        IOmnichannelChannelEndpointStore store,
        IPhoneNumberService phoneNumberService)
        => new(
            store,
            phoneNumberService,
            [],
            NullLogger<CatalogManager<OmnichannelChannelEndpoint>>.Instance);
}
