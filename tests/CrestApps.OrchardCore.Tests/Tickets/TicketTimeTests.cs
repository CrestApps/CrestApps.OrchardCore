using CrestApps.Core;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.TimeTracker;
using CrestApps.OrchardCore.TimeTracker;
using CrestApps.OrchardCore.TimeTracker.Core.Indexes;
using CrestApps.OrchardCore.TimeTracker.Core.Services;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// Time on tickets is kept by Time Tracker: starting work on a ticket switches the agent's time to the desk's ticket
/// stage for that ticket, another ticket is a new entry, stopping goes back to what they did before (or clocks out),
/// and each ticket adds up its time by person.
/// </summary>
public sealed class TicketTimeTests
{
    private static readonly DateTime _nine = new(2026, 11, 16, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task WorkOnTickets_IsTimeInTheDesksStage_AndStoppingGoesBackToWork()
    {
        // Arrange
        await using var kit = await Kit.CreateAsync();
        var t1 = Ticket("t1", "support");
        var t2 = Ticket("t2", "support");
        await kit.SwitchAsync(kit.WorkStageId, 0);

        // Act (9:00 work, 10:00 on T-1, 10:30 on T-2, 11:00 stop: back to work)
        kit.Now = _nine.AddMinutes(60);
        var started = await kit.RunAsync(time => time.StartAsync(t1, "ana", "Ana", "ana"));
        kit.Now = _nine.AddMinutes(90);
        await kit.RunAsync(time => time.StartAsync(t2, "ana", "Ana", "ana"));
        var whileOnT2 = await kit.RunAsync(time => time.SummarizeAsync("t2", "ana"));
        kit.Now = _nine.AddMinutes(120);
        var stopped = await kit.RunAsync(time => time.StopAsync("t2", "ana", "Ana", "ana"));
        var notOnIt = await kit.RunAsync(time => time.StopAsync("t2", "ana", "Ana", "ana"));
        var t1Time = await kit.RunAsync(time => time.SummarizeAsync("t1", "ana"));
        var t2Time = await kit.RunAsync(time => time.SummarizeAsync("t2", "ana"));

        // Assert
        Assert.True(started.Succeeded, string.Join(" ", started.Errors));
        Assert.Equal(kit.TicketStageId, started.Entry.StageId);
        Assert.Equal(TimeTrackerConstants.Sources.Ticket, started.Entry.Source);
        Assert.Equal("Ticket:t1", started.Entry.SourceRef);
        Assert.Equal(_nine.AddMinutes(90), whileOnT2.WorkingSinceUtc);
        Assert.True(stopped.Succeeded, string.Join(" ", stopped.Errors));
        Assert.Equal(kit.WorkStageId, stopped.Entry.StageId);
        Assert.Null(stopped.Entry.SourceRef);
        Assert.False(notOnIt.Succeeded);
        Assert.Equal(30, Assert.Single(t1Time.People).Minutes);
        Assert.Equal("Ana", t1Time.People[0].Name);
        Assert.Equal(30, t2Time.TotalMinutes);
        Assert.Null(t2Time.WorkingSinceUtc);
    }

    [Fact]
    public async Task StoppingWorkStartedOnATicket_ClocksOut_AndADeskWithoutAStageRecordsNothing()
    {
        // Arrange
        await using var kit = await Kit.CreateAsync();

        // Act
        var started = await kit.RunAsync(time => time.StartAsync(Ticket("t1", "support"), "ben", "Ben", "ben"));
        kit.Now = _nine.AddMinutes(45);
        var stopped = await kit.RunAsync(time => time.StopAsync("t1", "ben", "Ben", "ben"));
        var status = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().GetStatusAsync("ben"), commit: false);
        var noStage = await kit.RunAsync(time => time.StartAsync(Ticket("t3", "sales"), "ben", "Ben", "ben"));

        // Assert
        Assert.True(started.Succeeded, string.Join(" ", started.Errors));
        Assert.True(stopped.Succeeded, string.Join(" ", stopped.Errors));
        Assert.Null(status.Open);
        Assert.False(noStage.Succeeded);
    }

    [Fact]
    public async Task LoggedTime_TakesItsPlaceInTheAgentsDay_InTheBillableOrNotBilledStage_AndTheTicketListsIt()
    {
        // Arrange (Ana works from 9:00; at 11:00 she logs 30 minutes from 10:00, and 15 minutes not billed that just ended)
        await using var kit = await Kit.CreateAsync();
        var t1 = Ticket("t1", "support");
        await kit.SwitchAsync(kit.WorkStageId, 0);
        kit.Now = _nine.AddMinutes(120);

        // Act
        var billable = await kit.RunAsync(time => time.LogAsync(t1, new TicketTimeLog { UserId = "ana", UserName = "Ana", Actor = "ana", Minutes = 30, StartUtc = _nine.AddMinutes(60), Note = "Reset the router", Billable = true }));
        var notBilled = await kit.RunAsync(time => time.LogAsync(t1, new TicketTimeLog { UserId = "ana", UserName = "Ana", Actor = "ana", Minutes = 15, Note = "Internal handover", Billable = false }));
        var overlapping = await kit.RunAsync(time => time.LogAsync(t1, new TicketTimeLog { UserId = "ana", UserName = "Ana", Actor = "ana", Minutes = 20, StartUtc = _nine.AddMinutes(80) }));
        var tooLong = await kit.RunAsync(time => time.LogAsync(t1, new TicketTimeLog { UserId = "ana", UserName = "Ana", Actor = "ana", Minutes = 25 * 60 }));
        var summary = await kit.RunAsync(time => time.SummarizeAsync(t1, "ana"));
        var day = await kit.Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().ListAsync(new TimeEntryQuery { UserIds = ["ana"] }), commit: false);

        // Assert
        Assert.True(billable.Succeeded, string.Join(" ", billable.Errors));
        Assert.True(notBilled.Succeeded, string.Join(" ", notBilled.Errors));
        Assert.Equal(kit.TicketStageId, billable.Entry.StageId);
        Assert.Equal(kit.NotBilledStageId, notBilled.Entry.StageId);
        Assert.Equal(TimeTrackerConstants.Sources.Ticket, billable.Entry.Source);
        Assert.Equal("Ticket:t1", billable.Entry.SourceRef);
        Assert.Equal(_nine.AddMinutes(105), notBilled.Entry.StartUtc);
        Assert.StartsWith("It overlaps other time from", Assert.Single(overlapping.Errors));
        Assert.False(tooLong.Succeeded);
        Assert.Equal(45, summary.TotalMinutes);
        Assert.Equal(15, summary.NotBilledMinutes);
        Assert.Equal(["Internal handover", "Reset the router"], summary.Entries.Select(entry => entry.Note));
        Assert.Equal([false, true], summary.Entries.Select(entry => entry.Billable));
        Assert.True(summary.CanChooseBillable);
        Assert.Equal(
            [kit.WorkStageId, kit.TicketStageId, kit.WorkStageId, kit.NotBilledStageId, kit.WorkStageId],
            day.Select(entry => entry.StageId));
    }

    [Theory]
    [InlineData("30", 30)]
    [InlineData("45m", 45)]
    [InlineData("45 min", 45)]
    [InlineData("1h", 60)]
    [InlineData("1h 30m", 90)]
    [InlineData("1h30", 90)]
    [InlineData("2 hours 5 minutes", 125)]
    [InlineData("1:30", 90)]
    [InlineData("1.5h", 90)]
    [InlineData("1,5 h", 90)]
    [InlineData(" 0:05 ", 5)]
    [InlineData("", null)]
    [InlineData("0", null)]
    [InlineData("abc", null)]
    [InlineData("1:75", null)]
    [InlineData("-10", null)]
    public void TimeSpent_IsReadInTheWaysPeopleWriteIt(string value, int? minutes)
        => Assert.Equal(minutes, TicketTimeDuration.ParseMinutes(value));

    private static ContentItem Ticket(string id, string deskId)
    {
        var ticket = new ContentItem { ContentItemId = id, ContentType = "Ticket" };
        ticket.Apply(new TicketPart { DeskId = deskId });

        return ticket;
    }

    private sealed class Kit : IAsyncDisposable
    {
        public CalendarTestHarness Harness { get; private set; }

        public DateTime Now { get; set; } = _nine;

        public string WorkStageId { get; private set; }

        public string TicketStageId { get; private set; }

        public string NotBilledStageId { get; private set; }

        public static async Task<Kit> CreateAsync()
        {
            var kit = new Kit();
            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(() => kit.Now);
            var support = new TicketDesk { ItemId = "support", Name = "Support" };
            var sales = new TicketDesk { ItemId = "sales", Name = "Sales" };
            var desks = new Mock<ICatalog<TicketDesk>>();
            desks.Setup(value => value.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => id == "support" ? support : id == "sales" ? sales : null);

            kit.Harness = await CalendarTestHarness.CreateAsync(
                services =>
                {
                    services.AddTimeTrackerCore();
                    services.AddSingleton(clock.Object);
                    services.AddSingleton(new Mock<IExternalTimeClock>().Object);
                    services.AddSingleton(desks.Object);
                    services.AddScoped<TicketTime>();
                },
                TimeTrackerIndexSchema.CreateAsync,
                [new TimeTrackerIndexProvider(), new TimeCategoryIndexProvider(), new TimeStageIndexProvider()],
                [TimeTrackerConstants.CollectionName]);

            kit.WorkStageId = await kit.StageAsync("Work");
            kit.TicketStageId = await kit.StageAsync("Tickets");
            kit.NotBilledStageId = await kit.StageAsync("Tickets, not billed");
            support.Put(new TicketDeskTimeSettings { StageId = kit.TicketStageId, NotBilledStageId = kit.NotBilledStageId });

            return kit;
        }

        public Task<T> RunAsync<T>(Func<TicketTime, Task<T>> work)
            => Harness.RunAsync(services => work(services.GetRequiredService<TicketTime>()));

        public async Task SwitchAsync(string stageId, int minutesAfterNine)
        {
            Now = _nine.AddMinutes(minutesAfterNine);

            var result = await Harness.RunAsync(services => services.GetRequiredService<ITimeTrackerService>().SwitchAsync("ana", "Ana", stageId, TimeTrackerConstants.Sources.Self, "ana"));

            Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        }

        public ValueTask DisposeAsync()
            => Harness.DisposeAsync();

        private Task<string> StageAsync(string name)
            => Harness.RunAsync(async services =>
            {
                var manager = services.GetRequiredService<TimeStageManager>();
                var stage = await manager.NewAsync();

                stage.Name = name;
                await manager.CreateAsync(stage);

                return stage.ItemId;
            });
    }
}
