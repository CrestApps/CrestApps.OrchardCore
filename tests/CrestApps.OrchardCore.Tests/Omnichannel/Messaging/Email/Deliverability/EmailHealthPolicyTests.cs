using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email.Deliverability;

public sealed class EmailHealthPolicyTests
{
    private static readonly DateTime _start = new(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GetDailyLimit_WithoutWarmUp_IsTheConfiguredLimit()
    {
        Assert.Equal(2000, EmailHealthPolicy.GetDailyLimit(new EmailSendingLimits { MaxPerDay = 2000 }, _start));
        Assert.Equal(0, EmailHealthPolicy.GetDailyLimit(new EmailSendingLimits { MaxPerDay = 0 }, _start));
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 100)]
    [InlineData(3, 400)]
    [InlineData(5, 1600)]
    [InlineData(6, 2000)]
    [InlineData(40, 2000)]
    public void GetDailyLimit_WhileWarmingUp_DoublesEveryDayUpToTheConfiguredLimit(int day, int expected)
    {
        var limits = new EmailSendingLimits { MaxPerDay = 2000, WarmUp = true, WarmUpStartedUtc = _start, WarmUpFirstDayLimit = 50 };

        Assert.Equal(expected, EmailHealthPolicy.GetDailyLimit(limits, _start.AddDays(day).AddHours(1)));
    }

    [Fact]
    public void IsWarmingUp_EndsOnceTheDailyLimitIsReached()
    {
        var limits = new EmailSendingLimits { MaxPerDay = 2000, WarmUp = true, WarmUpStartedUtc = _start, WarmUpFirstDayLimit = 50 };

        Assert.True(EmailHealthPolicy.IsWarmingUp(limits, _start.AddDays(5)));
        Assert.False(EmailHealthPolicy.IsWarmingUp(limits, _start.AddDays(6)));
        Assert.False(EmailHealthPolicy.IsWarmingUp(new EmailSendingLimits(), _start));
    }

    [Fact]
    public void GetTurnInterval_IsTheAddressesBindingPace()
    {
        Assert.Equal(TimeSpan.FromSeconds(18), EmailHealthPolicy.GetTurnInterval(new EmailSendingLimits { MaxPerHour = 200, MinimumSecondsBetweenSends = 2 }, _start));
        Assert.Equal(TimeSpan.FromSeconds(30), EmailHealthPolicy.GetTurnInterval(new EmailSendingLimits { MaxPerHour = 200, MinimumSecondsBetweenSends = 30 }, _start));
        Assert.Equal(TimeSpan.FromSeconds(1), EmailHealthPolicy.GetTurnInterval(new EmailSendingLimits { MaxPerHour = 0, MinimumSecondsBetweenSends = 0 }, _start));

        // A warming address spreads its small allowance over twelve hours: 50 a day is one every 864 seconds.
        var warming = new EmailSendingLimits { MaxPerHour = 200, MaxPerDay = 2000, WarmUp = true, WarmUpStartedUtc = _start, WarmUpFirstDayLimit = 50 };
        Assert.Equal(TimeSpan.FromSeconds(864), EmailHealthPolicy.GetTurnInterval(warming, _start));
    }

    [Theory]
    [InlineData(50, 10, 0, EmailHealthLevel.Healthy)]
    [InlineData(200, 2, 0, EmailHealthLevel.Healthy)]
    [InlineData(200, 5, 0, EmailHealthLevel.Warning)]
    [InlineData(200, 10, 0, EmailHealthLevel.Poor)]
    [InlineData(150, 0, 1, EmailHealthLevel.Warning)]
    [InlineData(400, 0, 2, EmailHealthLevel.Poor)]
    public void Judge_UsesTheThresholdsMailboxProvidersBlockAt(int sent, int hardBounces, int complaints, EmailHealthLevel expected)
    {
        Assert.Equal(expected, EmailHealthPolicy.Judge(sent, hardBounces, complaints));
    }
}
