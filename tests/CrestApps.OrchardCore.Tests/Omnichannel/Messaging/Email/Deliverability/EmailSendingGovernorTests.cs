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
        var decision = await governor.EvaluateAsync(_address, Settings(), "ann@example.com", EmailSendKind.Reply, reserveTurn: true, TestContext.Current.CancellationToken);

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
        var decision = await governor.EvaluateAsync(_address, Settings(maxPerHour: 5), "bob@example.com", EmailSendKind.Reply, reserveTurn: true, TestContext.Current.CancellationToken);

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
        var decision = await governor.EvaluateAsync(_address, Settings(maxPerHour: 3), "bob@example.com", EmailSendKind.Bulk, reserveTurn: false, TestContext.Current.CancellationToken);

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
        var decision = await governor.EvaluateAsync(_address, Settings(maxPerHour: 0, maxPerDay: 2), "bob@example.com", EmailSendKind.Bulk, reserveTurn: false, TestContext.Current.CancellationToken);

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
        var decision = await governor.EvaluateAsync(_address, Settings(gapSeconds: 5), "bob@example.com", EmailSendKind.Bulk, reserveTurn: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(clock.UtcNow.AddSeconds(4), decision.RetryAfterUtc);
    }

    [Fact]
    public async Task EvaluateAsync_WhenSending_WaitsOutTheMinimumGapInsteadOfGoingToTheBackOfTheLine()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out var log, out _, out var states, out var clock);
        log.AddSent(AddressId, clock.UtcNow.AddMilliseconds(-1500));

        // Act
        var decision = await governor.EvaluateAsync(_address, Settings(gapSeconds: 2), "bob@example.com", EmailSendKind.Bulk, reserveTurn: true, TestContext.Current.CancellationToken);

        // Assert: the email goes once the gap has passed, and takes no turn at the back of the queue.
        Assert.True(decision.IsAllowed);
        Assert.Equal(0, states.Saves);
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
        var first = await governor.EvaluateAsync(_address, settings, "a@example.com", EmailSendKind.Bulk, reserveTurn: true, TestContext.Current.CancellationToken);
        var second = await governor.EvaluateAsync(_address, settings, "b@example.com", EmailSendKind.Bulk, reserveTurn: true, TestContext.Current.CancellationToken);
        var third = await governor.EvaluateAsync(_address, settings, "c@example.com", EmailSendKind.Bulk, reserveTurn: true, TestContext.Current.CancellationToken);

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
        var first = await governor.EvaluateAsync(_address, Settings(maxPerHour: 1), null, EmailSendKind.Bulk, reserveTurn: false, TestContext.Current.CancellationToken);
        var second = await governor.EvaluateAsync(_address, Settings(maxPerHour: 1), null, EmailSendKind.Bulk, reserveTurn: false, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(first.RetryAfterUtc, second.RetryAfterUtc);
        Assert.Equal(0, states.Saves);
    }

    [Fact]
    public async Task RecordFailureAsync_AThrottle_PausesBulkMailLongerEachTimeAndASendEndsTheRun()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out _, out _, out var states, out var clock);
        var start = clock.UtcNow;

        // Act
        var firstPause = await governor.RecordFailureAsync(_address, Settings(), "a@example.com", EmailFailureKind.Throttled, "421 4.7.0", isBulk: true, TestContext.Current.CancellationToken);

        // The server throttles the address again once the first pause is over.
        clock.UtcNow = firstPause.Value.AddMinutes(1);
        var secondStart = clock.UtcNow;
        var secondPause = await governor.RecordFailureAsync(_address, Settings(), "b@example.com", EmailFailureKind.Throttled, "421 4.7.0", isBulk: true, TestContext.Current.CancellationToken);

        var heldBulk = await governor.EvaluateAsync(_address, Settings(), "c@example.com", EmailSendKind.Bulk, reserveTurn: false, TestContext.Current.CancellationToken);
        var reply = await governor.EvaluateAsync(_address, Settings(), "c@example.com", EmailSendKind.Reply, reserveTurn: false, TestContext.Current.CancellationToken);

        clock.UtcNow = secondPause.Value.AddMinutes(1);
        await governor.RecordSentAsync(_address, "d@example.com", "m-1", isBulk: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(15), firstPause - start);
        Assert.Equal(TimeSpan.FromMinutes(30), secondPause - secondStart);
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
        var held = await governor.EvaluateAsync(_address, Settings(), "ann@example.com", EmailSendKind.Bulk, reserveTurn: false, TestContext.Current.CancellationToken);

        // Assert
        var state = states.States[AddressId];
        Assert.Equal(EmailSendingPauseKind.PoorHealth, state.PauseKind);
        Assert.Null(state.PausedUntilUtc);
        Assert.False(held.IsAllowed);

        // A throttle does not end a pause that waits for a person.
        await governor.PauseAsync(AddressId, EmailSendingPauseKind.Throttled, "421", TestContext.Current.CancellationToken);
        Assert.Equal(EmailSendingPauseKind.PoorHealth, state.PauseKind);

        Assert.True(await governor.ResumeAsync(AddressId, TestContext.Current.CancellationToken));
        Assert.True((await governor.EvaluateAsync(_address, Settings(maxPerHour: 0, maxPerDay: 0), "ann@example.com", EmailSendKind.Bulk, reserveTurn: false, TestContext.Current.CancellationToken)).IsAllowed);
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
        var toGmail = await governor.EvaluateAsync(_address, Settings(gapSeconds: 0), "ann@gmail.com", EmailSendKind.Bulk, reserveTurn: false, TestContext.Current.CancellationToken);
        var toOutlook = await governor.EvaluateAsync(_address, Settings(gapSeconds: 0), "bob@outlook.com", EmailSendKind.Bulk, reserveTurn: false, TestContext.Current.CancellationToken);

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
        var toGmail = await governor.EvaluateAsync(_address, settings, "c@gmail.com", EmailSendKind.Bulk, reserveTurn: false, TestContext.Current.CancellationToken);
        var toYahoo = await governor.EvaluateAsync(_address, settings, "c@yahoo.com", EmailSendKind.Bulk, reserveTurn: false, TestContext.Current.CancellationToken);

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
        var decision = await governor.EvaluateAsync(_address, settings, "ann@example.com", EmailSendKind.Bulk, reserveTurn: false, TestContext.Current.CancellationToken);

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

    // Live: one batch of five 5.7.x reports paused the address for 16 hours, each report escalating the last one's pause.
    [Fact]
    public async Task PauseAsync_ABatchOfBlockReports_PausesOnceWithoutEscalating()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out _, out _, out var states, out var clock);
        var pauses = new List<DateTime?>();

        // Act
        for (var i = 0; i < 5; i++)
        {
            pauses.Add(await governor.PauseAsync(AddressId, EmailSendingPauseKind.Blocked, "554 5.7.1 blocked", TestContext.Current.CancellationToken));
        }

        // Assert
        Assert.All(pauses, pause => Assert.Equal(clock.UtcNow.AddHours(1), pause));
        Assert.Equal(1, states.States[AddressId].ConsecutiveBlocks);
    }

    [Fact]
    public async Task PauseAsync_AThrottleDuringALongerBlock_LeavesTheBlockInPlace()
    {
        // Arrange: blocks have repeated, so the next one pauses for the longest block pause, a day.
        var governor = DeliverabilityTestFactory.CreateGovernor(out _, out _, out var states, out var clock);
        states.States[AddressId] = new EmailSendingState { ItemId = "state-1", AddressId = AddressId, ConsecutiveBlocks = 5 };

        // Act
        var block = await governor.PauseAsync(AddressId, EmailSendingPauseKind.Blocked, "554 5.7.1 blocked", TestContext.Current.CancellationToken);
        var throttle = await governor.PauseAsync(AddressId, EmailSendingPauseKind.Throttled, "421 4.7.0", TestContext.Current.CancellationToken);

        // Assert
        var state = states.States[AddressId];
        Assert.Equal(clock.UtcNow.AddHours(24), block);
        Assert.Equal(block, throttle);
        Assert.Equal(EmailSendingPauseKind.Blocked, state.PauseKind);
        Assert.Equal(block, state.PausedUntilUtc);
        Assert.Equal(0, state.ConsecutiveThrottles);
    }

    [Fact]
    public async Task PauseAsync_BuildsOnAPauseWrittenSinceTheStateWasRead()
    {
        // Arrange: a provider's report blocked the address after this scope had read its state.
        var governor = DeliverabilityTestFactory.CreateGovernor(out _, out _, out var states, out var clock);
        var blockedUntil = clock.UtcNow.AddHours(1);
        states.States[AddressId] = new EmailSendingState { ItemId = "state-1", AddressId = AddressId, PauseKind = EmailSendingPauseKind.Blocked, PausedUntilUtc = blockedUntil, PausedUtc = clock.UtcNow, ConsecutiveBlocks = 1 };
        states.ScopeCopies[AddressId] = new EmailSendingState { ItemId = "state-1", AddressId = AddressId };

        // Act
        var pause = await governor.PauseAsync(AddressId, EmailSendingPauseKind.Throttled, "421 4.7.0", TestContext.Current.CancellationToken);

        // Assert: the throttle's 15 minutes do not replace the hour-long block the database holds.
        Assert.Equal(blockedUntil, pause);
        Assert.Equal(EmailSendingPauseKind.Blocked, states.States[AddressId].PauseKind);
        Assert.Equal(0, states.Saves);
    }

    [Fact]
    public async Task EvaluateAsync_TakingATurn_NeverOverwritesAPauseWrittenSinceTheStateWasRead()
    {
        // Arrange: the address is at its hourly limit, and a block was written after this scope read the state.
        var governor = DeliverabilityTestFactory.CreateGovernor(out var log, out _, out var states, out var clock);
        log.AddSent(AddressId, clock.UtcNow.AddMinutes(-30));
        log.AddSent(AddressId, clock.UtcNow.AddMinutes(-1));

        var blockedUntil = clock.UtcNow.AddHours(1);
        states.States[AddressId] = new EmailSendingState { ItemId = "state-1", AddressId = AddressId, PauseKind = EmailSendingPauseKind.Blocked, PausedUntilUtc = blockedUntil, PausedUtc = clock.UtcNow, ConsecutiveBlocks = 1 };
        states.ScopeCopies[AddressId] = new EmailSendingState { ItemId = "state-1", AddressId = AddressId };

        // Act
        var decision = await governor.EvaluateAsync(_address, Settings(maxPerHour: 2, gapSeconds: 0), "a@example.com", EmailSendKind.Bulk, reserveTurn: true, TestContext.Current.CancellationToken);

        // Assert: the email waits for the block, and the block is not overwritten by a turn.
        Assert.Equal(blockedUntil, decision.RetryAfterUtc);
        Assert.Equal(EmailSendingPauseKind.Blocked, states.States[AddressId].PauseKind);
        Assert.Null(states.States[AddressId].NextBulkSlotUtc);
        Assert.Equal(0, states.Saves);
    }

    [Fact]
    public async Task RecordSentAsync_AReplyGettingThrough_DoesNotEndARunOfBlocks()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out _, out _, out var states, out var clock);
        await governor.PauseAsync(AddressId, EmailSendingPauseKind.Blocked, "554 5.7.1 blocked", TestContext.Current.CancellationToken);
        clock.UtcNow = clock.UtcNow.AddHours(2);

        // Act
        await governor.RecordSentAsync(_address, "ann@example.com", "m-1", isBulk: false, TestContext.Current.CancellationToken);
        var secondBlock = await governor.PauseAsync(AddressId, EmailSendingPauseKind.Blocked, "554 5.7.1 blocked", TestContext.Current.CancellationToken);

        // Assert: the second block in a row pauses for twice as long.
        Assert.Equal(clock.UtcNow.AddHours(2), secondBlock);
        Assert.Equal(2, states.States[AddressId].ConsecutiveBlocks);
    }

    [Fact]
    public async Task CheckHealthAsync_AfterResumingAPoorHealthPause_DoesNotCountTheBouncesBeforeIt()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out var log, out _, out var states, out var clock);
        AddHistory(log, clock.UtcNow, sent: 100, hardBounces: 5);
        await governor.CheckHealthAsync(_address, Settings(), TestContext.Current.CancellationToken);
        var resumed = clock.UtcNow;

        // Act
        Assert.True(await governor.ResumeAsync(AddressId, TestContext.Current.CancellationToken));
        clock.UtcNow = resumed.AddMinutes(10);
        await governor.CheckHealthAsync(_address, Settings(), TestContext.Current.CancellationToken);

        // Assert
        var state = states.States[AddressId];
        Assert.Equal(resumed, state.ResumedUtc);
        Assert.Equal(EmailSendingPauseKind.None, state.PauseKind);
    }

    [Fact]
    public async Task CheckHealthAsync_AfterResuming_PausesAgainWhenTheNewMailBounces()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out var log, out _, out var states, out var clock);
        AddHistory(log, clock.UtcNow, sent: 100, hardBounces: 5);
        await governor.CheckHealthAsync(_address, Settings(), TestContext.Current.CancellationToken);
        await governor.ResumeAsync(AddressId, TestContext.Current.CancellationToken);
        var resumed = clock.UtcNow;

        for (var i = 0; i < 100; i++)
        {
            log.AddSent(AddressId, resumed.AddMinutes(1));
        }

        for (var i = 0; i < 5; i++)
        {
            log.Entries.Add(new EmailDeliveryLogEntry { AddressId = AddressId, Kind = EmailDeliveryEventKind.HardBounce, OccurredUtc = resumed.AddMinutes(2) });
        }

        clock.UtcNow = resumed.AddMinutes(10);

        // Act
        await governor.CheckHealthAsync(_address, Settings(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(EmailSendingPauseKind.PoorHealth, states.States[AddressId].PauseKind);
    }

    [Fact]
    public async Task ResumeAsync_AThrottlePause_LeavesTheHealthMeasureAlone()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out _, out _, out var states, out _);
        await governor.PauseAsync(AddressId, EmailSendingPauseKind.Throttled, "421 4.7.0", TestContext.Current.CancellationToken);

        // Act
        var resumed = await governor.ResumeAsync(AddressId, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(resumed);
        Assert.Null(states.States[AddressId].ResumedUtc);
        Assert.Equal(EmailSendingPauseKind.None, states.States[AddressId].PauseKind);
    }

    [Fact]
    public async Task EvaluateAsync_AnAutomatedReplyPastTheHourlyLimit_WaitsForRoomWithoutTakingATurn()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out var log, out _, out var states, out var clock);
        var oldest = clock.UtcNow.AddMinutes(-40);
        log.AddSent(AddressId, oldest);
        log.AddSent(AddressId, clock.UtcNow.AddMinutes(-20), isBulk: false);
        log.AddSent(AddressId, clock.UtcNow.AddMinutes(-5));

        // Act
        var decision = await governor.EvaluateAsync(_address, Settings(maxPerHour: 3), "bob@example.com", EmailSendKind.Automated, reserveTurn: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(decision.IsAllowed);
        Assert.Equal(oldest.AddHours(1), decision.RetryAfterUtc);
        Assert.Equal(0, states.Saves);
    }

    [Fact]
    public async Task EvaluateAsync_AnAutomatedReply_WaitsForAPause()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out _, out _, out _, out _);
        var pause = await governor.PauseAsync(AddressId, EmailSendingPauseKind.Throttled, "421 4.7.0", TestContext.Current.CancellationToken);

        // Act
        var decision = await governor.EvaluateAsync(_address, Settings(), "bob@example.com", EmailSendKind.Automated, reserveTurn: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(pause, decision.RetryAfterUtc);
    }

    [Fact]
    public async Task EvaluateAsync_AnAutomatedReply_IsNotHeldByTheBulkGapOrADomainsWait()
    {
        // Arrange
        var governor = DeliverabilityTestFactory.CreateGovernor(out var log, out _, out _, out var clock);
        log.AddSent(AddressId, clock.UtcNow.AddSeconds(-1));

        for (var i = 0; i < 3; i++)
        {
            log.Entries.Add(new EmailDeliveryLogEntry { AddressId = AddressId, Kind = EmailDeliveryEventKind.Deferred, RecipientDomain = "gmail.com", OccurredUtc = clock.UtcNow.AddMinutes(-i) });
        }

        await governor.NoteDomainDeferralAsync(AddressId, "gmail.com", TestContext.Current.CancellationToken);

        // Act
        var decision = await governor.EvaluateAsync(_address, Settings(gapSeconds: 60), "ann@gmail.com", EmailSendKind.Automated, reserveTurn: true, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(decision.IsAllowed);
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
