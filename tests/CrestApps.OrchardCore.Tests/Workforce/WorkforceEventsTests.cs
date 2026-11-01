using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Workforce.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The events Workforce raises for workflows: time off asked for and decided, schedules published, punches,
/// timesheets submitted and approved, attendance exceptions and points thresholds, each with the ids a workflow
/// needs; and the publisher that starts workflows with them.
/// </summary>
public sealed class WorkforceEventsTests
{
    private static readonly DateOnly _monday = TimeClockKit.Monday;

    [Fact]
    public async Task TimeOff_RaisesRequestedApprovedAndDeclined()
    {
        var events = new RecordingEvents();
        await using var harness = await TimeOffKit.CreateAsync(services => services.AddSingleton<IWorkforceEvents>(events));
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
        var type = await TimeOffKit.AddTypeAsync(harness, "Jury duty", item => item.RequiresBalance = false);
        var first = await TimeOffKit.SubmitAsync(harness, ana, type, _monday);
        var second = await TimeOffKit.SubmitAsync(harness, ana, type, _monday.AddDays(7));

        await harness.RunAsync(services => services.GetRequiredService<ITimeOffRequestService>().ApproveAsync(first.Value.ItemId, "manager", null));
        await harness.RunAsync(services => services.GetRequiredService<ITimeOffRequestService>().DeclineAsync(second.Value.ItemId, "manager", "Busy week"));

        Assert.Equal(
            [WorkforceEvents.TimeOffRequested, WorkforceEvents.TimeOffRequested, WorkforceEvents.TimeOffApproved, WorkforceEvents.TimeOffDeclined],
            events.Raised.Select(item => item.Name));
        Assert.All(events.Raised, item => Assert.Equal(ana.ItemId, item.Input["WorkerId"]));
        Assert.Equal(second.Value.ItemId, events.Raised[^1].Input["RequestId"]);
    }

    [Fact]
    public async Task Publishing_RaisesSchedulePublished()
    {
        var events = new RecordingEvents();
        await using var harness = await WorkforceTests.CreateAsync(services => services.AddSingleton<IWorkforceEvents>(events));
        var team = await WorkforceTests.AddTeamAsync(harness, "Front desk", TeamKind.Team);
        var ana = await PlanningKit.AddMemberAsync(harness, "Ana", team);
        var period = await PlanningKit.CreatePeriodAsync(harness, team);

        await PlanningKit.AddDraftAsync(harness, period, ana.ItemId, _monday, new TimeOnly(9, 0), new TimeOnly(17, 0));
        await PlanningKit.PublishAsync(harness, period);

        var published = Assert.Single(events.Raised);
        Assert.Equal(WorkforceEvents.SchedulePublished, published.Name);
        Assert.Equal(period.ItemId, published.Input["PeriodId"]);
        Assert.Equal(team.ItemId, published.Input["TeamId"]);
    }

    [Fact]
    public async Task TheTimeClock_RaisesPunchesTimesheetsExceptionsAndPoints()
    {
        var events = new RecordingEvents();
        await using var kit = await TimeClockKit.CreateAsync(services => services.AddSingleton<IWorkforceEvents>(events));
        var team = await kit.AddTeamAsync("Front desk", policy =>
        {
            policy.AttendancePoints = [new AttendancePointRule { Kind = AttendanceExceptionKind.MissedPunch, Points = 1 }];
            policy.PointsThreshold = 1;
        });
        var ana = await PlanningKit.AddMemberAsync(kit.Harness, "Ana", team);

        await kit.WorkAsync(ana, _monday, 9, 17);
        await kit.PunchAtAsync(ana, TimePunchKind.In, _monday.AddDays(1), 9);
        kit.Now = TimeClockKit.Local(_monday.AddDays(3), 9);

        var timesheet = await kit.TimesheetAsync(ana);

        await kit.Harness.RunAsync(services => services.GetRequiredService<ITimesheetService>().SubmitAsync(timesheet.ItemId, "Right.", "ana"));
        await kit.Harness.RunAsync(services => services.GetRequiredService<ITimesheetService>().ApproveAsync(timesheet.ItemId, "manager"));

        Assert.Equal(
            [
                WorkforceEvents.PunchedIn,
                WorkforceEvents.PunchedOut,
                WorkforceEvents.PunchedIn,
                WorkforceEvents.AttendanceExceptionRaised,
                WorkforceEvents.AttendancePointsThresholdReached,
                WorkforceEvents.TimesheetSubmitted,
                WorkforceEvents.TimesheetApproved,
            ],
            events.Raised.Select(item => item.Name));
        Assert.Equal(timesheet.ItemId, events.Raised[^1].Input["TimesheetId"]);
        Assert.Equal(nameof(AttendanceExceptionKind.MissedPunch), events.Raised[3].Input["Kind"]);
    }

    [Fact]
    public async Task ThePublisher_StartsWorkflowsWithTheEventsInput()
    {
        var manager = new Mock<IWorkflowManager>();
        var publisher = new WorkflowWorkforceEvents(manager.Object);
        var input = new Dictionary<string, object> { ["WorkerId"] = "w1" };

        await publisher.RaiseAsync(WorkforceEvents.PunchedIn, input, "p1", TestContext.Current.CancellationToken);

        manager.Verify(item => item.TriggerEventAsync(WorkforceEvents.PunchedIn, It.Is<IDictionary<string, object>>(value => (string)value["WorkerId"] == "w1"), "p1", false, false), Times.Once);
    }

    private sealed class RecordingEvents : IWorkforceEvents
    {
        public List<(string Name, IDictionary<string, object> Input)> Raised { get; } = [];

        public Task RaiseAsync(string name, IDictionary<string, object> input, string correlationId, CancellationToken cancellationToken = default)
        {
            Raised.Add((name, input));

            return Task.CompletedTask;
        }
    }
}
