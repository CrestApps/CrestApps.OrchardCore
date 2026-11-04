using CrestApps.OrchardCore.BusinessHours.Services;
using CrestApps.OrchardCore.Outreach;
using CrestApps.OrchardCore.Outreach.Core.Services;
using CrestApps.OrchardCore.Outreach.Models;
using CrestApps.OrchardCore.WorkQueues;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Outreach;

/// <summary>
/// The outreach engine sends a run one audience page at a time on the leased work queue: it skips recipients a policy
/// rules out, retries a failed batch without sending twice, gives up on a batch that keeps failing, and holds a batch
/// while the run's channel is in quiet hours.
/// </summary>
public sealed class OutreachEngineTests
{
    [Fact]
    public async Task ARun_IsSentOnePageAtATime_UntilTheAudienceEnds()
    {
        // Arrange
        var source = new ListAudienceSource([.. Enumerable.Range(1, 7).Select(index => ListAudienceSource.Recipient($"+1555000000{index}"))]);
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(source, sender);
        var run = await harness.StartAsync(NewRun(pageSize: 3));

        // Act
        await harness.ProcessDueAsync();

        // Assert
        var stored = await harness.GetRunAsync(run.ItemId);
        Assert.Equal(OutreachRunStatus.Completed, stored.Status);
        Assert.Equal((7, 0, 0, 3), (stored.Sent, stored.Skipped, stored.Failed, stored.BatchesCompleted));
        Assert.NotNull(stored.CompletedUtc);
        Assert.Equal([null, "3", "6"], source.Cursors);
        Assert.Equal(7, sender.Sent.Select(send => send.Address).Distinct().Count());
        Assert.All(await harness.GetBatchesAsync(run.ItemId), batch => Assert.Equal(LeasedWorkItemStatus.Completed, batch.Status));
    }

    [Fact]
    public async Task ARecipientAPolicyRulesOut_OrTheSourceSkips_IsCountedAsSkipped()
    {
        // Arrange
        var source = new ListAudienceSource(
            ListAudienceSource.Recipient("+15550000001"),
            ListAudienceSource.Recipient("+15550000002"),
            ListAudienceSource.Recipient("+15550000003", OutreachConstants.SkipReasons.NoConsent),
            ListAudienceSource.Recipient(string.Empty));
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(source, sender, services => services.AddSingleton<IOutreachRecipientPolicy>(new DenyListPolicy("+15550000002")));
        var run = await harness.StartAsync(NewRun());

        // Act
        await harness.ProcessDueAsync();

        // Assert
        var stored = await harness.GetRunAsync(run.ItemId);
        Assert.Equal(["+15550000001"], sender.Sent.Select(send => send.Address));
        Assert.Equal((1, 3, 0), (stored.Sent, stored.Skipped, stored.Failed));
    }

    [Fact]
    public async Task ARefusedSend_IsCountedAsFailed_AndTheRunCarriesOn()
    {
        // Arrange
        var source = new ListAudienceSource(ListAudienceSource.Recipient("+15550000001"), ListAudienceSource.Recipient("+15550000002"));
        var sender = new RecordingSender();
        sender.Refuses.Add("+15550000001");
        await using var harness = await CreateAsync(source, sender);
        var run = await harness.StartAsync(NewRun());

        // Act
        await harness.ProcessDueAsync();

        // Assert
        var stored = await harness.GetRunAsync(run.ItemId);
        Assert.Equal((1, 0, 1, OutreachRunStatus.Completed), (stored.Sent, stored.Skipped, stored.Failed, stored.Status));
    }

    [Fact]
    public async Task AFailedBatch_IsRetriedAfterItsBackoff_WithoutSendingTwice()
    {
        // Arrange
        var source = new ListAudienceSource(ListAudienceSource.Recipient("+15550000001"), ListAudienceSource.Recipient("+15550000002"), ListAudienceSource.Recipient("+15550000003"));
        var sender = new RecordingSender();
        sender.Throws["+15550000002"] = 1;
        await using var harness = await CreateAsync(source, sender);
        var run = await harness.StartAsync(NewRun());
        var batchId = (await harness.GetBatchesAsync(run.ItemId)).Single().ItemId;

        // Act
        var first = await harness.ProcessBatchAsync(batchId);
        var afterFailure = (await harness.GetBatchesAsync(run.ItemId)).Single();
        var tooSoon = await harness.ProcessBatchAsync(batchId);
        harness.Clock.UtcNow = afterFailure.NextAttemptUtc;
        var second = await harness.ProcessBatchAsync(batchId);

        // Assert
        Assert.Equal(OutreachBatchOutcome.Retrying, first);
        Assert.Equal(1, afterFailure.AttemptCount);
        Assert.Equal(["+15550000001"], afterFailure.ProcessedKeys);
        Assert.Equal(OutreachBatchOutcome.NotDue, tooSoon);
        Assert.Equal(OutreachBatchOutcome.Completed, second);
        Assert.Equal(["+15550000001", "+15550000002", "+15550000003"], sender.Sent.Select(send => send.Address));

        var stored = await harness.GetRunAsync(run.ItemId);
        Assert.Equal((3, OutreachRunStatus.Completed), (stored.Sent, stored.Status));
    }

    [Fact]
    public async Task ABatchThatKeepsFailing_FailsTheRun()
    {
        // Arrange
        var source = new ListAudienceSource(ListAudienceSource.Recipient("+15550000001"));
        var sender = new RecordingSender();
        sender.Throws["+15550000001"] = -1;
        var observer = new RecordingObserver();
        await using var harness = await CreateAsync(source, sender, services => services.AddSingleton<IOutreachRunObserver>(observer));
        var run = await harness.StartAsync(NewRun());
        var batchId = (await harness.GetBatchesAsync(run.ItemId)).Single().ItemId;

        // Act
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await harness.ProcessBatchAsync(batchId);
            harness.Clock.UtcNow = harness.Clock.UtcNow.AddHours(1);
        }

        // Assert
        var stored = await harness.GetRunAsync(run.ItemId);
        Assert.Equal(OutreachRunStatus.Failed, stored.Status);
        Assert.Equal("The provider is down.", stored.LastError);
        Assert.Equal(LeasedWorkItemStatus.DeadLettered, (await harness.GetBatchesAsync(run.ItemId)).Single().Status);
        Assert.Equal(OutreachRunStatus.Failed, observer.Updates[^1].Status);
    }

    [Fact]
    public async Task InQuietHours_TheBatchWaitsForTheScheduleToOpen_WithoutUsingAnAttempt()
    {
        // Arrange
        var source = new ListAudienceSource(ListAudienceSource.Recipient("+15550000001"));
        var sender = new RecordingSender { QuietHours = true };
        var hours = new OpensLaterHoursService();
        await using var harness = await CreateAsync(source, sender, services => services.AddSingleton<IOpenHoursService>(hours));
        hours.OpensUtc = harness.Clock.UtcNow.AddHours(2);
        var run = NewRun();
        run.SendingHoursScheduleId = "texting-hours";
        run = await harness.StartAsync(run);
        var batchId = (await harness.GetBatchesAsync(run.ItemId)).Single().ItemId;

        // Act
        var outcome = await harness.ProcessBatchAsync(batchId);
        var waiting = (await harness.GetBatchesAsync(run.ItemId)).Single();
        harness.Clock.UtcNow = hours.OpensUtc;
        var later = await harness.ProcessBatchAsync(batchId);

        // Assert
        Assert.Equal(OutreachBatchOutcome.Deferred, outcome);
        Assert.Equal(hours.OpensUtc, waiting.NextAttemptUtc);
        Assert.Equal(0, waiting.AttemptCount);
        Assert.Equal("QuietHours", waiting.LastError);
        Assert.Equal(OutreachBatchOutcome.Completed, later);
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task AChannelThatDoesNotObserveQuietHours_SendsOutsideTheSchedule()
    {
        // Arrange
        var source = new ListAudienceSource(ListAudienceSource.Recipient("someone@example.com"));
        var sender = new RecordingSender { QuietHours = false };
        var hours = new OpensLaterHoursService();
        await using var harness = await CreateAsync(source, sender, services => services.AddSingleton<IOpenHoursService>(hours));
        hours.OpensUtc = harness.Clock.UtcNow.AddHours(2);
        var run = NewRun();
        run.SendingHoursScheduleId = "texting-hours";
        await harness.StartAsync(run);

        // Act
        await harness.ProcessDueAsync();

        // Assert
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task ACanceledRun_SendsNothingMore()
    {
        // Arrange
        var source = new ListAudienceSource(ListAudienceSource.Recipient("+15550000001"));
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(source, sender);
        var run = await harness.StartAsync(NewRun());

        // Act
        var canceled = await harness.CancelAsync(run.ItemId);
        await harness.ProcessDueAsync();

        // Assert
        Assert.True(canceled);
        Assert.Empty(sender.Sent);
        Assert.Equal(OutreachRunStatus.Canceled, (await harness.GetRunAsync(run.ItemId)).Status);
        Assert.Equal(LeasedWorkItemStatus.Completed, (await harness.GetBatchesAsync(run.ItemId)).Single().Status);
    }

    [Fact]
    public async Task AMissingSender_HoldsTheRunWithoutUsingAnAttempt()
    {
        // Arrange
        var source = new ListAudienceSource(ListAudienceSource.Recipient("+15550000001"));
        await using var harness = await OutreachTestHarness.CreateAsync(services => services.AddSingleton<IOutreachAudienceSource>(source));
        var run = await harness.StartAsync(NewRun());
        var batchId = (await harness.GetBatchesAsync(run.ItemId)).Single().ItemId;

        // Act
        var outcome = await harness.ProcessBatchAsync(batchId);

        // Assert
        var batch = (await harness.GetBatchesAsync(run.ItemId)).Single();
        Assert.Equal(OutreachBatchOutcome.Deferred, outcome);
        Assert.Equal(0, batch.AttemptCount);
        Assert.Equal(LeasedWorkItemStatus.Pending, batch.Status);
    }

    [Fact]
    public async Task ARunStartedWithProgress_ResumesAtItsCursor_AndKeepsItsCounts()
    {
        // Arrange
        var source = new ListAudienceSource([.. Enumerable.Range(1, 4).Select(index => ListAudienceSource.Recipient($"+1555000000{index}"))]);
        var sender = new RecordingSender();
        await using var harness = await CreateAsync(source, sender);
        var run = NewRun();
        run.Cursor = "2";
        run.Sent = 2;
        run = await harness.StartAsync(run);

        // Act
        await harness.ProcessDueAsync();

        // Assert
        var stored = await harness.GetRunAsync(run.ItemId);
        Assert.Equal(["+15550000003", "+15550000004"], sender.Sent.Select(send => send.Address));
        Assert.Equal(4, stored.Sent);
    }

    private static Task<OutreachTestHarness> CreateAsync(ListAudienceSource source, RecordingSender sender, Action<IServiceCollection> configure = null)
        => OutreachTestHarness.CreateAsync(services =>
        {
            services.AddSingleton<IOutreachAudienceSource>(source);
            services.AddSingleton<IOutreachChannelSender>(sender);
            configure?.Invoke(services);
        });

    private static OutreachRun NewRun(int pageSize = 10)
        => new()
        {
            Name = "Test run",
            OwnerType = "Test",
            OwnerId = "owner-1",
            AudienceSource = "List",
            Sender = "Recording",
            PageSize = pageSize,
            Template = new OutreachTemplate { Body = "Hello" },
        };
}
