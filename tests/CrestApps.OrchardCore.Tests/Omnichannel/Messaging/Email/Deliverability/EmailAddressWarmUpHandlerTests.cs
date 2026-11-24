using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email.Deliverability;

/// <summary>
/// An address imported with its warm-up turned on but no start date stayed on its first day's limit for good, because a
/// missing start is read as today. Every save of an address now dates it.
/// </summary>
public sealed class EmailAddressWarmUpHandlerTests
{
    [Fact]
    public async Task CreatingAsync_AWarmUpWithoutAStartDate_StartsNow()
    {
        // Arrange
        var clock = new DeliverabilityClock();
        var endpoint = Address(new EmailSendingLimits { WarmUp = true, WarmUpFirstDayLimit = 50, MaxPerDay = 2000 });

        // Act
        await new EmailAddressWarmUpHandler(clock).CreatingAsync(new CreatingContext<OmnichannelChannelEndpoint>(endpoint), TestContext.Current.CancellationToken);

        // Assert
        var limits = Limits(endpoint);

        Assert.Equal(clock.UtcNow, limits.WarmUpStartedUtc);

        // Three days later the limit has doubled three times, where an undated warm-up stayed at its first day's limit.
        Assert.Equal(400, EmailHealthPolicy.GetDailyLimit(limits, clock.UtcNow.AddDays(3)));
    }

    [Fact]
    public async Task UpdatingAsync_KeepsAWarmUpsStartDate()
    {
        // Arrange
        var clock = new DeliverabilityClock();
        var started = clock.UtcNow.AddDays(-4);
        var endpoint = Address(new EmailSendingLimits { WarmUp = true, WarmUpStartedUtc = started });

        // Act
        await new EmailAddressWarmUpHandler(clock).UpdatingAsync(new UpdatingContext<OmnichannelChannelEndpoint>(endpoint, new JsonObject()), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(started, Limits(endpoint).WarmUpStartedUtc);
    }

    [Fact]
    public async Task UpdatingAsync_LeavesAnAddressThatIsNotWarmingUpUndated()
    {
        // Arrange
        var endpoint = Address(new EmailSendingLimits { WarmUp = false });

        // Act
        await new EmailAddressWarmUpHandler(new DeliverabilityClock()).UpdatingAsync(new UpdatingContext<OmnichannelChannelEndpoint>(endpoint, new JsonObject()), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(Limits(endpoint).WarmUpStartedUtc);
    }

    private static EmailSendingLimits Limits(OmnichannelChannelEndpoint endpoint)
    {
        Assert.True(endpoint.TryGet<EmailAddressSettings>(out var settings));

        return settings.Limits;
    }

    private static OmnichannelChannelEndpoint Address(EmailSendingLimits limits)
    {
        var endpoint = new OmnichannelChannelEndpoint
        {
            ItemId = "address-1",
            Value = "news@contoso.com",
            Capabilities = [OmnichannelConstants.Channels.Email],
        };

        endpoint.Put(new EmailAddressSettings { Limits = limits });

        return endpoint;
    }
}
