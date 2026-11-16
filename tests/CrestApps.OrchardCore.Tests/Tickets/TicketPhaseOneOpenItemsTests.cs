using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.BusinessHours.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels.Core.Services;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Core;
using CrestApps.OrchardCore.Timeline.Core.Services;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The open items of phase 1: a file no timeline entry kept a day after it was stored (its request failed after storing
/// it) is removed, and a newer one or a kept one is left alone; a removed record's timeline is deleted by a resumable
/// step that runs again when it fails; and a timer whose schedule became never open counts around the clock (neither
/// freezing nor running late forever), says it has no business hours, and the ticket notes it in its history.
/// </summary>
public sealed class TicketPhaseOneOpenItemsTests
{
    [Fact]
    public async Task AFileNoEntryKept_IsRemovedAfterTheGracePeriod_AndAKeptOneIsNot()
    {
        // Arrange: "kept" belongs to an entry of t1; "orphan" belongs to no entry; both are a day old.
        var attachments = new Mock<IAttachmentService>();
        var before = DateTime.MinValue;
        attachments.Setup(service => service.ListUploadedBeforeAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyCollection<string> _, DateTime uploadedBeforeUtc, DateTime? _, int _, CancellationToken _) => before = uploadedBeforeUtc)
            .ReturnsAsync((IReadOnlyCollection<string> kinds, DateTime _, DateTime? from, int _, CancellationToken _) => kinds.Contains(TicketsConstants.RecordKind)
                ? [Attachment("kept"), Attachment("orphan")]
                : []);
        attachments.Setup(service => service.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var provider = new Mock<ITimelineAccessProvider>();
        provider.SetupGet(value => value.SubjectKind).Returns(TicketsConstants.RecordKind);
        await using var harness = await TicketReportDataSourceTests.CreateAsync(services =>
        {
            services.AddTimelineCore();
            services.AddSingleton(attachments.Object);
            services.AddSingleton(provider.Object);
        });
        await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<ISession>().SaveAsync(new TimelineEntry
            {
                ItemId = "e1",
                Subject = new RecordRef(TicketsConstants.RecordKind, "t1"),
                Kind = TicketsConstants.EntryKinds.Reply,
                Author = new PartyRef(PartyKinds.User, "ana"),
                Body = "See the log.",
                AttachmentIds = ["kept"],
                CreatedUtc = CalendarTestHarness.Now.AddDays(-1),
            }, false, TimelineConstants.CollectionName);

            return true;
        });

        // Act
        var removed = await harness.RunAsync(services => services.GetRequiredService<TimelineOrphanFileSweeper>().SweepAsync());

        // Assert
        Assert.Equal(1, removed);
        Assert.Equal(CalendarTestHarness.Now - TimelineOrphanFileSweeper.GracePeriod, before);
        attachments.Verify(service => service.DeleteAsync("orphan", It.IsAny<CancellationToken>()), Times.Once());
        attachments.Verify(service => service.DeleteAsync("kept", It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task NoFileIsRemoved_WhileTheOlderEntriesFilesAreStillBeingIndexed()
    {
        // Arrange: after an upgrade, the entries made before the file index existed are indexed by a step; until it is
        // done, their files are missing from the index and would look like files no entry kept.
        var attachments = new Mock<IAttachmentService>();
        attachments.Setup(service => service.ListUploadedBeforeAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<DateTime>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Attachment("older")]);
        attachments.Setup(service => service.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var provider = new Mock<ITimelineAccessProvider>();
        provider.SetupGet(value => value.SubjectKind).Returns(TicketsConstants.RecordKind);
        await using var harness = await TicketReportDataSourceTests.CreateAsync(services =>
        {
            services.AddTimelineCore();
            services.AddSingleton(attachments.Object);
            services.AddSingleton(provider.Object);
        });
        await harness.RunAsync(async services =>
        {
            await services.GetRequiredService<ResumableSteps>().RequestAsync(TimelineFileIndexStep.StepName);

            return true;
        });

        // Act
        var removed = await harness.RunAsync(services => services.GetRequiredService<TimelineOrphanFileSweeper>().SweepAsync());

        // Assert
        Assert.Equal(0, removed);
        attachments.Verify(service => service.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task ARemovedRecordsTimeline_IsDeletedByTheStep_WhichRunsAgainWhenItFails()
    {
        // Arrange
        var timeline = new Mock<ITimelineService>();
        var fail = true;
        timeline.Setup(service => service.DeleteAllAsync(It.IsAny<RecordRef>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((RecordRef subject, CancellationToken _) => fail && subject.Id == "t2" ? throw new InvalidOperationException("The store is down.") : 1);
        var step = new TimelineDeletionStep(timeline.Object);
        var context = new ResumableStepContext { Data = new Dictionary<string, string>(StringComparer.Ordinal) { ["subject:Ticket:t1"] = "1", ["subject:Ticket:t2"] = "1" } };

        // Act: the first run fails on t2 (the runner keeps the step and its data for the next pass); the next one works.
        await Assert.ThrowsAsync<InvalidOperationException>(() => step.RunAsync(new ResumableStepContext { Data = new Dictionary<string, string>(context.Data, StringComparer.Ordinal) }, TestContext.Current.CancellationToken));
        fail = false;
        var done = await step.RunAsync(context, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(done);
        Assert.Empty(context.Data);
        timeline.Verify(service => service.DeleteAllAsync(new RecordRef(TicketsConstants.RecordKind, "t2"), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ATimerOnAScheduleThatNeverOpens_CountsAroundTheClock_AndSaysSo()
    {
        // Arrange: "closed" has no open hours; around the clock (no schedule) counts every hour.
        var start = CalendarTestHarness.Now;
        var hours = new Mock<IOpenHoursService>();
        hours.Setup(service => service.GetOpenWindowsAsync("closed", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        hours.Setup(service => service.GetOpenTimeBetweenAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string schedule, DateTime from, DateTime to, string _, CancellationToken _) => schedule is null ? to - from : TimeSpan.Zero);
        hours.Setup(service => service.AddOpenTimeAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<TimeSpan>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, DateTime from, TimeSpan duration, string _, CancellationToken _) => from + duration);
        var clock = new ServiceLevelClock(hours.Object);
        var timer = new ServiceLevelTimer { HoursScheduleId = "closed", TargetSeconds = 3600, State = ServiceLevelTimerState.Running, StartedUtc = start };

        // Act
        await clock.ScheduleAsync(timer, start, TestContext.Current.CancellationToken);
        var usedAfterHalfAnHour = await clock.GetUsedSecondsAsync(timer, start.AddMinutes(30), TestContext.Current.CancellationToken);

        // Assert
        Assert.True(timer.NoBusinessHours);
        Assert.Equal(start.AddHours(1), timer.DueUtc);
        Assert.Equal(1800, usedAfterHalfAnHour);
    }

    [Fact]
    public async Task ATicketsHistory_SaysWhyItsTargetCountsAroundTheClock()
    {
        // Arrange
        var timeline = new Mock<ITimelineService>();
        TimelineEntry added = null;
        timeline.Setup(service => service.AddAsync(It.IsAny<TimelineEntry>(), It.IsAny<CancellationToken>()))
            .Callback((TimelineEntry entry, CancellationToken _) => added = entry)
            .ReturnsAsync(new TimelineResult());
        var services = new ServiceCollection().AddSingleton(timeline.Object).BuildServiceProvider();
        var observer = new TicketServiceLevelObserver(Mock.Of<IContentManager>(), [], NullLogger<TicketServiceLevelObserver>.Instance, services, new PassThroughStringLocalizer<TicketServiceLevelObserver>());

        // Act
        await observer.NoBusinessHoursAsync(new ServiceLevelTimer { Subject = new RecordRef(TicketsConstants.RecordKind, "t1"), Metric = ServiceLevelMetrics.FirstResponse, NoBusinessHours = true }, TestContext.Current.CancellationToken);
        await observer.NoBusinessHoursAsync(new ServiceLevelTimer { Subject = new RecordRef("WorkOrder", "w1"), NoBusinessHours = true }, TestContext.Current.CancellationToken);

        // Assert: the note names its target, since a ticket's other targets get their own note.
        Assert.Equal((new RecordRef(TicketsConstants.RecordKind, "t1"), TicketsConstants.EntryKinds.Event, TimelineVisibility.Internal), (added.Subject, added.Kind, added.Visibility));
        Assert.Contains("first response target", added.Body, StringComparison.Ordinal);
        timeline.Verify(service => service.AddAsync(It.IsAny<TimelineEntry>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    private static Attachment Attachment(string id)
        => new() { ItemId = id, Owner = new RecordRef(TicketsConstants.RecordKind, "t1"), FileName = id + ".txt", UploadedUtc = CalendarTestHarness.Now.AddDays(-1) };
}
