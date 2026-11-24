using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Dispatch;
using CrestApps.OrchardCore.Dispatch.Core.Services;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Tests.Workforce;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Dispatch;

public sealed class DispatchOptimizationTests
{
    private static readonly GeoPoint _west = new(42.36, -71.30);
    private static readonly GeoPoint _east = new(42.36, -70.90);

    [Fact]
    public async Task TheOptimizer_CutsTheTravelOfANaivePlan_AndKeepsFixedVisits()
    {
        // Two technicians at opposite ends of town, each given the other's jobs; a pinned visit stays put.
        var day = new DateTime(2026, 11, 9, 13, 0, 0, DateTimeKind.Utc);
        var open = new List<(DateTime, DateTime)> { (day, day.AddHours(8)) };
        var problem = new DispatchProblem
        {
            BufferMinutes = 5,
            TravelMinutes = (from, to) => from is { } a && to is { } b ? (int)Math.Round(Math.Abs(a.Longitude - b.Longitude) * 100) : 0,
            Workers =
            [
                new ProblemWorker { Id = "west", Home = _west, Open = open },
                new ProblemWorker { Id = "east", Home = _east, Open = open },
            ],
            Visits =
            [
                new ProblemVisit { Id = "w1", Place = _west, DurationMinutes = 60, CurrentWorkerId = "east", CurrentStartUtc = day },
                new ProblemVisit { Id = "w2", Place = _west, DurationMinutes = 60, CurrentWorkerId = "east", CurrentStartUtc = day.AddHours(2) },
                new ProblemVisit { Id = "e1", Place = _east, DurationMinutes = 60, CurrentWorkerId = "west", CurrentStartUtc = day },
                new ProblemVisit { Id = "e2", Place = _east, DurationMinutes = 60, CurrentWorkerId = "west", CurrentStartUtc = day.AddHours(2) },
                new ProblemVisit { Id = "pinned", Place = _east, DurationMinutes = 30, FixedWorkerId = "west", FixedStartUtc = day.AddHours(6) },
                new ProblemVisit { Id = "new", Place = _west, DurationMinutes = 30 },
            ],
        };
        var current = problem.Visits.Where(visit => visit.CurrentWorkerId is not null).ToDictionary(visit => visit.Id, visit => (visit.CurrentWorkerId, visit.CurrentStartUtc.Value));
        var before = DefaultDispatchOptimizer.TravelOf(problem, current);

        var plan = await new DefaultDispatchOptimizer().SolveAsync(problem, TestContext.Current.CancellationToken);
        var stops = plan.Stops.ToDictionary(stop => stop.VisitId);

        Assert.True(plan.TravelMinutes < before, $"{plan.TravelMinutes} should be less than {before}");
        Assert.Equal("west", stops["w1"].WorkerId);
        Assert.Equal("west", stops["w2"].WorkerId);
        Assert.Equal("east", stops["e1"].WorkerId);
        Assert.Equal("west", stops["pinned"].WorkerId);
        Assert.Equal(day.AddHours(6), stops["pinned"].StartUtc);
        Assert.Equal("west", stops["new"].WorkerId);
        Assert.Empty(plan.Unassigned);
    }

    [Fact]
    public async Task AProposal_SwapsTheDays_AndApplyingItMovesEveryVisitOnce()
    {
        // Arrange (Ana starts at the depot and Ben 20 km south; each has the other's jobs)
        await using var kit = await DispatchKit.CreateAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");
        var ben = await kit.AddTechnicianAsync("Ben Field", worker => worker.HomeBase = new CrestApps.OrchardCore.Staff.Models.StaffLocation { Latitude = DispatchKit.South.Latitude, Longitude = DispatchKit.South.Longitude });

        await kit.PublishShiftsAsync(ana, ben);

        var (_, south1) = await kit.AddJobAsync("South one", DispatchKit.South);
        var (_, south2) = await kit.AddJobAsync("South two", DispatchKit.South);
        var (_, depot1) = await kit.AddJobAsync("Depot one", DispatchKit.Depot);

        Assert.True((await kit.AssignAsync(south1, DispatchKit.Monday(9), ana)).Succeeded);
        Assert.True((await kit.AssignAsync(south2, DispatchKit.Monday(11), ana)).Succeeded);
        Assert.True((await kit.AssignAsync(depot1, DispatchKit.Monday(9), ben)).Succeeded);

        // Act
        var proposal = await kit.Harness.RunAsync(services => services.GetRequiredService<IDispatchOptimizationService>().ProposeAsync(PlanningKit.Monday, null, false), commit: false);
        var applied = await kit.Harness.RunAsync(services => services.GetRequiredService<IDispatchOptimizationService>().ApplyAsync(proposal.Moves.ToList(), "dispatcher"));
        var after = await kit.Harness.RunAsync(services => services.GetRequiredService<IDispatchService>().ListScheduledAsync(DispatchKit.Monday(0), DispatchKit.Monday(23)), commit: false);

        // Assert
        Assert.True(proposal.TravelAfter < proposal.TravelBefore, $"{proposal.TravelAfter} should be less than {proposal.TravelBefore}");
        Assert.Empty(applied.Errors);
        Assert.Equal(proposal.Moves.Count, applied.Applied.Count);
        Assert.Equal(3, after.Count);
        Assert.All(after.Where(visit => visit.Summary.StartsWith("South", StringComparison.Ordinal)), visit => Assert.Equal(ben.ItemId, visit.LeadWorkerId));
        Assert.Equal(ana.ItemId, after.Single(visit => visit.Summary == "Depot one").LeadWorkerId);
        Assert.Equal(3, (await kit.JobEventsAsync()).Count);
    }

    [Fact]
    public async Task AJobForTwo_NeedsTwoPeople_AndEquipmentIsNeverDoubleBooked()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var lift = await kit.Harness.CreateCalendarAsync("Equipment", "Lift 1");

        kit.Equipment.Add(new DispatchEquipment("lift-1", "Lift 1", lift.ItemId));
        await kit.UpdateTypeAsync(type =>
        {
            type.CrewSize = 2;
            type.RequiredEquipmentTypeIds = ["lift"];
        });

        var ana = await kit.AddTechnicianAsync("Ana Field");
        var ben = await kit.AddTechnicianAsync("Ben Field");
        var cy = await kit.AddTechnicianAsync("Cy Field");
        var dee = await kit.AddTechnicianAsync("Dee Field");

        await kit.PublishShiftsAsync(ana, ben, cy, dee);

        var (_, first) = await kit.AddJobAsync("Roof unit", DispatchKit.Depot);
        var (_, second) = await kit.AddJobAsync("Other roof unit", DispatchKit.Depot);

        var alone = await kit.AssignAsync(first, DispatchKit.Monday(10), ana);
        var pair = await kit.AssignAsync(first, DispatchKit.Monday(10), ana, ben);
        var noLift = await kit.AssignAsync(second, DispatchKit.Monday(10, 30), cy, dee);
        var later = await kit.AssignAsync(second, DispatchKit.Monday(13), cy, dee);
        var reservations = await kit.Harness.GetOccurrencesAsync(new CalendarOccurrenceQuery { CalendarIds = [lift.ItemId], FromUtc = DispatchKit.Monday(0), ToUtc = DispatchKit.Monday(23) });

        Assert.Equal("This job needs 2 people.", Assert.Single(alone.Errors));
        Assert.True(pair.Succeeded, string.Join(" ", pair.Errors));
        Assert.Equal(["lift-1"], pair.Visit.EquipmentAssetIds);
        Assert.Equal("No lift is free then.", Assert.Single(noLift.Errors));
        Assert.True(later.Succeeded, string.Join(" ", later.Errors));
        Assert.Equal(2, reservations.Count);
    }

    [Fact]
    public async Task EnRoute_GivesAnEta_ATrackingLinkThatExpiresWhenDone_AndTheCustomerIsToldOnce()
    {
        // Arrange (the customer is 20 minutes from the depot)
        await using var kit = await DispatchKit.CreateAsync();

        kit.Settings.NoticesEnabled = true;

        var ana = await kit.AddTechnicianAsync("Ana Field");

        await kit.PublishShiftsAsync(ana);

        var (_, visit) = await kit.AddJobAsync("No heat", DispatchKit.South, workOrder => workOrder.Contact = new CrestApps.OrchardCore.WorkOrders.Models.WorkOrderContact { Name = "Pat", Phone = "+15555550100" });

        // Act
        Assert.True((await kit.AssignAsync(visit, DispatchKit.Monday(10), ana)).Succeeded);
        Assert.True((await kit.AssignAsync(visit, DispatchKit.Monday(10), ana)).Succeeded);

        kit.Now = DispatchKit.Monday(9, 30);

        var enRoute = await kit.MoveAsync(visit, VisitStatus.EnRoute);
        var token = await kit.Harness.RunAsync(async services => services.GetRequiredService<IVisitTrackingLinks>().CreateToken(await services.GetRequiredService<IDispatchService>().FindAsync(visit.ItemId)), commit: false);
        var tracked = await kit.Harness.RunAsync(services => services.GetRequiredService<IVisitTrackingLinks>().ReadAsync(token), commit: false);

        await kit.MoveAsync(visit, VisitStatus.OnSite);
        await kit.MoveAsync(visit, VisitStatus.Completed);

        var expired = await kit.Harness.RunAsync(services => services.GetRequiredService<IVisitTrackingLinks>().ReadAsync(token), commit: false);
        var tampered = await kit.Harness.RunAsync(services => services.GetRequiredService<IVisitTrackingLinks>().ReadAsync(token[..^2] + "xx"), commit: false);

        // Assert
        Assert.Equal(DispatchKit.Monday(9, 50), enRoute.Visit.EtaUtc);
        Assert.Equal(visit.ItemId, tracked?.ItemId);
        Assert.Null(expired);
        Assert.Null(tampered);
        Assert.Equal([DispatchConstants.Notices.Scheduled, DispatchConstants.Notices.EnRoute, DispatchConstants.Notices.Completed], kit.Sent.Select(message => message.Kind));
        Assert.StartsWith("Acme Heating: your visit V-000001 is on Monday 9 November, arriving between 09:00 and 11:00.", kit.Sent[0].Text);
        Assert.Contains("Ana is on the way, expected at 09:50. Follow along: https://example.test/track/", kit.Sent[1].Text);
    }

    [Fact]
    public async Task AVisitNotStartedInTime_IsAtRisk_Once_AndClearedWhenItStarts()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");

        await kit.PublishShiftsAsync(ana);

        var (_, visit) = await kit.AddJobAsync("No heat", DispatchKit.Depot);

        Assert.True((await kit.AssignAsync(visit, DispatchKit.Monday(10), ana)).Succeeded);

        kit.Now = DispatchKit.Monday(10, 10);

        var early = await RunRiskAsync(kit);

        kit.Now = DispatchKit.Monday(10, 30);

        var late = await RunRiskAsync(kit);
        var again = await RunRiskAsync(kit);
        var flagged = await kit.Harness.RunAsync(services => services.GetRequiredService<IDispatchService>().FindAsync(visit.ItemId), commit: false);

        await kit.MoveAsync(visit, VisitStatus.EnRoute);

        var cleared = await kit.Harness.RunAsync(services => services.GetRequiredService<IDispatchService>().FindAsync(visit.ItemId), commit: false);

        Assert.Equal(0, early);
        Assert.Equal(1, late);
        Assert.Equal(1, again);
        Assert.Equal("Not started: it was to start at 10:00.", flagged.AtRiskReason);
        Assert.Single(kit.Events, entry => entry.EventName == DispatchConstants.Events.AtRisk);
        Assert.Null(cleared.AtRiskReason);
    }

    private static Task<int> RunRiskAsync(DispatchKit kit)
        => kit.Harness.RunAsync(services => services.GetRequiredService<VisitRiskMonitor>().RunAsync());
}
