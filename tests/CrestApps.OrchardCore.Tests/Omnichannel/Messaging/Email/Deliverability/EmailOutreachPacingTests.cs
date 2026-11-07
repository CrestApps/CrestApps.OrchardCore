using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email.Deliverability;

public sealed class EmailOutreachPacingTests
{
    [Fact]
    public async Task Screener_AnActivityToASuppressedAddress_IsCancelledBeforeAnythingIsSent()
    {
        // Arrange
        var suppressions = new InMemoryEmailSuppressionList();
        await suppressions.SuppressAsync("gone@example.com", EmailSuppressionReason.Complaint, null, cancellationToken: TestContext.Current.CancellationToken);

        var screener = new EmailSuppressionScreener(suppressions);

        // Act
        var refused = await screener.ScreenAsync(new OmnichannelActivity { PreferredDestination = "Gone@Example.com" }, TestContext.Current.CancellationToken);
        var allowed = await screener.ScreenAsync(new OmnichannelActivity { PreferredDestination = "ann@example.com" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(OmnichannelConstants.Channels.Email, screener.Channel);
        Assert.False(refused.IsAllowed);
        Assert.Equal(OmnichannelConstants.TerminalReasons.AddressUndeliverable, refused.Reason);
        Assert.True(allowed.IsAllowed);
    }

    [Fact]
    public async Task Pacer_AnAddressAtItsLimit_GivesTheActivityATurnLater()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out var log, out _, out var states, out var clock);
        var address = new OmnichannelChannelEndpoint { ItemId = "address-1", Value = "news@contoso.com", Capabilities = [OmnichannelConstants.Channels.Email] };
        address.Put(new EmailAddressSettings { Limits = new EmailSendingLimits { MaxPerHour = 1, MinimumSecondsBetweenSends = 0 } });
        log.AddSent("address-1", clock.UtcNow.AddMinutes(-15));

        var pacer = new EmailSendPacer(governor);

        // Act
        var reserved = await pacer.GetBulkSendTimeAsync(address, reserveTurn: true, TestContext.Current.CancellationToken);
        var next = await pacer.GetBulkSendTimeAsync(address, reserveTurn: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(clock.UtcNow.AddMinutes(45), reserved);
        Assert.Equal(reserved.Value.AddHours(1), next);
        Assert.Equal(next.Value.AddHours(1), states.States["address-1"].NextBulkSlotUtc);
    }

    [Fact]
    public async Task Pacer_AnAddressWithRoom_SendsNow()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out _, out _, out _, out _);
        var address = new OmnichannelChannelEndpoint { ItemId = "address-1", Value = "news@contoso.com", Capabilities = [OmnichannelConstants.Channels.Email] };

        // Act & Assert
        Assert.Null(await new EmailSendPacer(governor).GetBulkSendTimeAsync(address, reserveTurn: true, TestContext.Current.CancellationToken));
    }
}
