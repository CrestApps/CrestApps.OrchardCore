using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Core.Omnichannel.Services;

/// <summary>
/// Caller IDs were typed, so a dialer profile or a provider could present a number the business does not own or a
/// mistyped one. They are now picked from the addresses used for voice calls.
/// </summary>
public sealed class OmnichannelCallerIdOptionsTests
{
    [Fact]
    public async Task OffersTheNumbersUsedForCalls_ByName()
    {
        // Arrange
        var manager = Manager(
            Address("Support line", "+15550000002", OmnichannelConstants.Channels.Phone),
            Address("Texts only", "+15550000003", OmnichannelConstants.Channels.Sms),
            Address("Main line", "+15550000001", OmnichannelConstants.Channels.Phone, OmnichannelConstants.Channels.Sms));

        // Act
        var options = await manager.GetCallerIdOptionsAsync(null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["Main line (+15550000001)", "Support line (+15550000002)"], options.Select(option => option.Text));
        Assert.Equal(["+15550000001", "+15550000002"], options.Select(option => option.Value));
        Assert.DoesNotContain(options, option => option.Selected);
    }

    [Fact]
    public async Task SelectsTheStoredNumber()
    {
        // Arrange
        var manager = Manager(
            Address("Main line", "+15550000001", OmnichannelConstants.Channels.Phone),
            Address("Support line", "+15550000002", OmnichannelConstants.Channels.Phone));

        // Act
        var options = await manager.GetCallerIdOptionsAsync(" +15550000002 ", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("+15550000002", Assert.Single(options, option => option.Selected).Value);
    }

    [Fact]
    public async Task KeepsAStoredNumberThatIsNotAnAddress_SoSavingDoesNotChangeIt()
    {
        // Arrange
        var manager = Manager(Address("Main line", "+15550000001", OmnichannelConstants.Channels.Phone));

        // Act
        var options = await manager.GetCallerIdOptionsAsync("+15559999999", TestContext.Current.CancellationToken);

        // Assert
        var kept = Assert.Single(options, option => option.Selected);
        Assert.Equal("+15559999999", kept.Value);
        Assert.Equal(2, options.Count);
    }

    [Fact]
    public async Task ShowsANumberWithoutAName_AsTheNumber()
    {
        // Arrange
        var manager = Manager(Address("+15550000001", "+15550000001", OmnichannelConstants.Channels.Phone));

        // Act
        var options = await manager.GetCallerIdOptionsAsync(null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("+15550000001", Assert.Single(options).Text);
    }

    private static IOmnichannelChannelEndpointManager Manager(params OmnichannelChannelEndpoint[] addresses)
    {
        var manager = new Mock<IOmnichannelChannelEndpointManager>();
        manager.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(addresses);

        return manager.Object;
    }

    private static OmnichannelChannelEndpoint Address(string name, string number, params string[] capabilities)
        => new()
        {
            ItemId = number,
            DisplayText = name,
            Value = number,
            AddressType = OmnichannelAddressTypes.PhoneNumber,
            Capabilities = [.. capabilities],
        };
}
