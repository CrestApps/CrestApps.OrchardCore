using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Deliverability;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.Email.Deliverability;

public sealed class EmailSendingGovernorTests
{
    private const string AddressId = "address-1";

    private static readonly OmnichannelChannelEndpoint _address = new()
    {
        ItemId = AddressId,
        Value = "news@contoso.com",
        Capabilities = [OmnichannelConstants.Channels.Email],
    };

    [Fact]
    public async Task EvaluateAsync_ASuppressedRecipient_IsRefusedEvenForAReply()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out _, out var suppressions, out _, out _);
        await suppressions.SuppressAsync("ann@example.com", EmailSuppressionReason.HardBounce, "5.1.1", cancellationToken: TestContext.Current.CancellationToken);

        // Act
        var decision = await governor.EvaluateAsync(_address, Settings(), "ann@example.com", isBulk: false, reserveTurn: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.IsRefused);
        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public async Task EvaluateAsync_AReply_IsNeverHeldBackByTheLimits()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out var log, out _, out _, out var clock);

        for (var i = 0; i < 5; i++)
        {
            log.AddSent(AddressId, clock.UtcNow.AddMinutes(-10 + i));
        }

        // Act
        var decision = await governor.EvaluateAsync(_address, Settings(maxPerHour: 5), "bob@example.com", isBulk: false, reserveTurn: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.IsAllowed);
    }

    [Fact]
    public async Task EvaluateAsync_BulkMailPastTheHourlyLimit_WaitsUntilTheOldestEmailLeavesTheHour()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out var log, out _, out _, out var clock);
        var oldest = clock.UtcNow.AddMinutes(-40);

        log.AddSent(AddressId, oldest);
        log.AddSent(AddressId, clock.UtcNow.AddMinutes(-20));
        log.AddSent(AddressId, clock.UtcNow.AddMinutes(-5));

        // Act
        var decision = await governor.EvaluateAsync(_address, Settings(maxPerHour: 3), "bob@example.com", isBulk: true, reserveTurn: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(oldest.AddHours(1), decision.RetryAfterUtc);
    }

    [Fact]
    public async Task EvaluateAsync_BulkMailPastTheDailyLimit_WaitsForTheDayWindow()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out var log, out _, out _, out var clock);
        var oldest = clock.UtcNow.AddHours(-20);

        log.AddSent(AddressId, oldest);
        log.AddSent(AddressId, clock.UtcNow.AddHours(-2));

        // Act
        var decision = await governor.EvaluateAsync(_address, Settings(maxPerHour: 0, maxPerDay: 2), "bob@example.com", isBulk: true, reserveTurn: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(oldest.AddDays(1), decision.RetryAfterUtc);
    }

    [Fact]
    public async Task EvaluateAsync_TheMinimumGap_HoldsTheNextBulkEmail()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out var log, out _, out _, out var clock);
        log.AddSent(AddressId, clock.UtcNow.AddSeconds(-1));

        // Act
        var decision = await governor.EvaluateAsync(_address, Settings(gapSeconds: 5), "bob@example.com", isBulk: true, reserveTurn: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(clock.UtcNow.AddSeconds(4), decision.RetryAfterUtc);
    }

    [Fact]
    public async Task EvaluateAsync_ReservingTurns_SpreadsHeldBackMailAtTheAddressesPace()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out var log, out _, out _, out var clock);
        var oldest = clock.UtcNow.AddMinutes(-30);
        log.AddSent(AddressId, oldest);
        log.AddSent(AddressId, clock.UtcNow.AddMinutes(-1));

        var settings = Settings(maxPerHour: 2, gapSeconds: 0);

        // Act
        var first = await governor.EvaluateAsync(_address, settings, "a@example.com", isBulk: true, reserveTurn: true, TestContext.Current.CancellationToken);
        var second = await governor.EvaluateAsync(_address, settings, "b@example.com", isBulk: true, reserveTurn: true, TestContext.Current.CancellationToken);
        var third = await governor.EvaluateAsync(_address, settings, "c@example.com", isBulk: true, reserveTurn: true, TestContext.Current.CancellationToken);

        // Assert: two an hour is one turn every 30 minutes.
        Assert.Equal(oldest.AddHours(1), first.RetryAfterUtc);
        Assert.Equal(first.RetryAfterUtc.Value.AddMinutes(30), second.RetryAfterUtc);
        Assert.Equal(second.RetryAfterUtc.Value.AddMinutes(30), third.RetryAfterUtc);
    }

    [Fact]
    public async Task EvaluateAsync_OnlyAsking_GivesOutNoTurns()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out var log, out _, out var states, out var clock);
        log.AddSent(AddressId, clock.UtcNow.AddMinutes(-30));

        // Act
        var first = await governor.EvaluateAsync(_address, Settings(maxPerHour: 1), null, isBulk: true, reserveTurn: false, TestContext.Current.CancellationToken);
        var second = await governor.EvaluateAsync(_address, Settings(maxPerHour: 1), null, isBulk: true, reserveTurn: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(first.RetryAfterUtc, second.RetryAfterUtc);
        Assert.Equal(0, states.Saves);
    }

    [Fact]
    public async Task RecordFailureAsync_AThrottle_PausesBulkMailLongerEachTimeAndASendEndsTheRun()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out _, out _, out var states, out var clock);

        // Act
        var firstPause = await governor.RecordFailureAsync(_address, Settings(), "a@example.com", EmailFailureKind.Throttled, "421 4.7.0", isBulk: true, TestContext.Current.CancellationToken);
        var secondPause = await governor.RecordFailureAsync(_address, Settings(), "b@example.com", EmailFailureKind.Throttled, "421 4.7.0", isBulk: true, TestContext.Current.CancellationToken);

        var heldBulk = await governor.EvaluateAsync(_address, Settings(), "c@example.com", isBulk: true, reserveTurn: false, TestContext.Current.CancellationToken);
        var reply = await governor.EvaluateAsync(_address, Settings(), "c@example.com", isBulk: false, reserveTurn: false, TestContext.Current.CancellationToken);

        clock.UtcNow = secondPause.Value.AddMinutes(1);
        await governor.RecordSentAsync(_address, "d@example.com", "m-1", isBulk: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(15), firstPause - new DeliverabilityClock().UtcNow);
        Assert.Equal(TimeSpan.FromMinutes(30), secondPause - new DeliverabilityClock().UtcNow);
        Assert.Equal(secondPause, heldBulk.RetryAfterUtc);
        Assert.True(reply.IsAllowed);
        Assert.Equal(0, states.States[AddressId].ConsecutiveThrottles);
        Assert.Equal(EmailSendingPauseKind.None, states.States[AddressId].PauseKind);
    }

    [Fact]
    public async Task RecordFailureAsync_AHardBounce_SuppressesTheRecipientAndLogsIt()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out var log, out var suppressions, out _, out _);

        // Act
        var pause = await governor.RecordFailureAsync(_address, Settings(), "gone@example.com", EmailFailureKind.HardBounce, "550 5.1.1 User unknown", isBulk: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(pause);
        Assert.Equal(EmailSuppressionReason.HardBounce, suppressions.Items["gone@example.com"].Reason);
        Assert.Contains(log.Entries, entry => entry.Kind == EmailDeliveryEventKind.HardBounce && entry.Recipient == "gone@example.com");
    }

    [Fact]
    public async Task RecordFailureAsync_ABlock_PausesBulkMailForAnHour()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out _, out var suppressions, out var states, out var clock);

        // Act
        var pause = await governor.RecordFailureAsync(_address, Settings(), "ann@example.com", EmailFailureKind.Blocked, "554 5.7.1 blocked", isBulk: true, TestContext.Current.CancellationToken);

        // Assert: the recipient did nothing wrong, so nothing is suppressed.
        Assert.Equal(clock.UtcNow.AddHours(1), pause);
        Assert.Equal(EmailSendingPauseKind.Blocked, states.States[AddressId].PauseKind);
        Assert.Empty(suppressions.Items);
    }

    [Fact]
    public async Task CheckHealthAsync_ABounceRateAtFivePercent_PausesBulkMailUntilResumed()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out var log, out _, out var states, out var clock);
        AddHistory(log, clock.UtcNow, sent: 100, hardBounces: 5);

        // Act
        await governor.CheckHealthAsync(_address, Settings(), TestContext.Current.CancellationToken);
        var held = await governor.EvaluateAsync(_address, Settings(), "ann@example.com", isBulk: true, reserveTurn: false, TestContext.Current.CancellationToken);

        // Assert
        var state = states.States[AddressId];
        Assert.Equal(EmailSendingPauseKind.PoorHealth, state.PauseKind);
        Assert.Null(state.PausedUntilUtc);
        Assert.False(held.IsAllowed);

        // A throttle does not end a pause that waits for a person.
        await governor.PauseAsync(AddressId, EmailSendingPauseKind.Throttled, "421", TestContext.Current.CancellationToken);
        Assert.Equal(EmailSendingPauseKind.PoorHealth, state.PauseKind);

        Assert.True(await governor.ResumeAsync(AddressId, TestContext.Current.CancellationToken));
        Assert.True((await governor.EvaluateAsync(_address, Settings(maxPerHour: 0, maxPerDay: 0), "ann@example.com", isBulk: true, reserveTurn: false, TestContext.Current.CancellationToken)).IsAllowed);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenTheAddressDoesNotAskForIt_NeverPauses()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out var log, out _, out var states, out var clock);
        AddHistory(log, clock.UtcNow, sent: 100, hardBounces: 20);

        var settings = Settings();
        settings.Limits.PauseOnPoorHealth = false;

        // Act
        await governor.CheckHealthAsync(_address, settings, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(states.States);
    }

    [Fact]
    public async Task NoteDomainDeferralAsync_ADomainThatKeepsDeferring_HoldsOnlyMailToThatDomain()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out var log, out _, out _, out var clock);

        for (var i = 0; i < 3; i++)
        {
            log.Entries.Add(new EmailDeliveryLogEntry { AddressId = AddressId, Kind = EmailDeliveryEventKind.Deferred, RecipientDomain = "gmail.com", OccurredUtc = clock.UtcNow.AddMinutes(-i) });
        }

        // Act
        await governor.NoteDomainDeferralAsync(AddressId, "gmail.com", TestContext.Current.CancellationToken);
        var toGmail = await governor.EvaluateAsync(_address, Settings(gapSeconds: 0), "ann@gmail.com", isBulk: true, reserveTurn: false, TestContext.Current.CancellationToken);
        var toOutlook = await governor.EvaluateAsync(_address, Settings(gapSeconds: 0), "bob@outlook.com", isBulk: true, reserveTurn: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(clock.UtcNow.Add(EmailHealthPolicy.DomainBackoff), toGmail.RetryAfterUtc);
        Assert.True(toOutlook.IsAllowed);
    }

    [Fact]
    public async Task EvaluateAsync_TheHourlyLimitPerDomain_HoldsOnlyThatDomain()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out var log, out _, out _, out var clock);
        log.AddSent(AddressId, clock.UtcNow.AddMinutes(-50), "a@gmail.com");
        log.AddSent(AddressId, clock.UtcNow.AddMinutes(-10), "b@gmail.com");

        var settings = Settings(gapSeconds: 0);
        settings.Limits.MaxPerHourPerDomain = 2;

        // Act
        var toGmail = await governor.EvaluateAsync(_address, settings, "c@gmail.com", isBulk: true, reserveTurn: false, TestContext.Current.CancellationToken);
        var toYahoo = await governor.EvaluateAsync(_address, settings, "c@yahoo.com", isBulk: true, reserveTurn: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(clock.UtcNow.AddMinutes(10), toGmail.RetryAfterUtc);
        Assert.True(toYahoo.IsAllowed);
    }

    [Fact]
    public async Task EvaluateAsync_AWarmingAddress_StopsAtTheDaysAllowance()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out var log, out _, out _, out var clock);
        var settings = Settings(maxPerHour: 0, gapSeconds: 0);
        settings.Limits.WarmUp = true;
        settings.Limits.WarmUpStartedUtc = clock.UtcNow.AddHours(-2);
        settings.Limits.WarmUpFirstDayLimit = 2;

        log.AddSent(AddressId, clock.UtcNow.AddMinutes(-90));
        log.AddSent(AddressId, clock.UtcNow.AddMinutes(-30));

        // Act
        var decision = await governor.EvaluateAsync(_address, settings, "ann@example.com", isBulk: true, reserveTurn: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(clock.UtcNow.AddMinutes(-90).AddDays(1), decision.RetryAfterUtc);
    }

    [Fact]
    public async Task GetHealthAsync_ReportsTheWeeksCountsAndTheDaysLimit()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out var log, out _, out _, out var clock);
        AddHistory(log, clock.UtcNow, sent: 200, hardBounces: 5);

        // Act
        var health = await governor.GetHealthAsync(_address, Settings(maxPerDay: 2000), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(200, health.Sent);
        Assert.Equal(5, health.HardBounces);
        Assert.Equal(EmailHealthLevel.Warning, health.Level);
        Assert.Equal(2000, health.DailyLimit);
        Assert.False(health.IsPaused);
    }

    private static void AddHistory(InMemoryEmailDeliveryLog log, DateTime now, int sent, int hardBounces)
    {
        for (var i = 0; i < sent; i++)
        {
            log.AddSent(AddressId, now.AddHours(-30).AddMinutes(i));
        }

        for (var i = 0; i < hardBounces; i++)
        {
            log.Entries.Add(new EmailDeliveryLogEntry { AddressId = AddressId, Kind = EmailDeliveryEventKind.HardBounce, OccurredUtc = now.AddHours(-29) });
        }
    }

    private static EmailAddressSettings Settings(int maxPerHour = 200, int maxPerDay = 2000, int gapSeconds = 2)
        => new()
        {
            Limits = new EmailSendingLimits
            {
                MaxPerHour = maxPerHour,
                MaxPerDay = maxPerDay,
                MinimumSecondsBetweenSends = gapSeconds,
            },
        };
}
