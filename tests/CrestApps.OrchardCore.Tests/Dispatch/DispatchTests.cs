using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Dispatch;
using CrestApps.OrchardCore.Dispatch.Core;
using CrestApps.OrchardCore.Dispatch.Core.Services;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Tests.Workforce;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Dispatch;

public sealed class DispatchTests
{
    [Fact]
    public void TheStatusRules_AllowOnlyTheUsualMoves()
    {
        Assert.True(VisitStatusRules.CanMove(VisitStatus.Scheduled, VisitStatus.EnRoute));
        Assert.True(VisitStatusRules.CanMove(VisitStatus.OnSite, VisitStatus.Incomplete));
        Assert.False(VisitStatusRules.CanMove(VisitStatus.Unscheduled, VisitStatus.OnSite));
        Assert.False(VisitStatusRules.CanMove(VisitStatus.Completed, VisitStatus.OnSite));
        Assert.True(VisitStatusRules.CanAssign(VisitStatus.Dispatched));
        Assert.False(VisitStatusRules.CanAssign(VisitStatus.OnSite));
        Assert.False(VisitStatusRules.KeepsWorkersBusy(VisitStatus.Canceled));
    }

    [Fact]
    public void Subtract_TakesBreaksAndTimeOffOutOfAShift()
    {
        var shift = new TimeSpanUtc(DispatchKit.Monday(8), DispatchKit.Monday(16));

        var open = DispatchResources.Subtract([shift], [new TimeSpanUtc(DispatchKit.Monday(12), DispatchKit.Monday(12, 30)), new TimeSpanUtc(DispatchKit.Monday(15), DispatchKit.Monday(17))]);

        Assert.Equal([new TimeSpanUtc(DispatchKit.Monday(8), DispatchKit.Monday(12)), new TimeSpanUtc(DispatchKit.Monday(12, 30), DispatchKit.Monday(15))], open);
    }

    [Fact]
    public async Task ANewWorkOrder_GetsAnUnscheduledVisit_WithItsNumberAndLength()
    {
        await using var kit = await DispatchKit.CreateAsync();

        var (workOrder, visit) = await kit.AddJobAsync("No heat", DispatchKit.South, workOrder => workOrder.Priority = WorkOrderPriority.High);
        var (_, second) = await kit.AddJobAsync("Noisy fan", DispatchKit.Depot);
        var unscheduled = await kit.Harness.RunAsync(services => services.GetRequiredService<IDispatchService>().ListUnscheduledAsync(), commit: false);

        Assert.Equal("V-000001", visit.Number);
        Assert.Equal("V-000002", second.Number);
        Assert.Equal(VisitStatus.Unscheduled, visit.Status);
        Assert.Equal(workOrder.Number, visit.WorkOrderNumber);
        Assert.Equal(60, visit.DurationMinutes);

        // The high priority one first.
        Assert.Equal([visit.ItemId, second.ItemId], unscheduled.Select(item => item.ItemId));
    }

    [Fact]
    public async Task WorkOrdersMadeWithoutVisits_GetThemOnce_WhenAsked()
    {
        await using var kit = await DispatchKit.CreateAsync();

        kit.Settings.CreateVisitsForNewWorkOrders = false;

        var workOrder = await kit.Harness.RunAsync(async services =>
        {
            var service = services.GetRequiredService<WorkOrderService>();
            var created = await service.NewAsync(kit.Type.ItemId);

            created.Summary = "Made before dispatch";

            return (await service.CreateAsync(created, "dispatcher")).WorkOrder;
        });
        var missing = await kit.Harness.RunAsync(services => services.GetRequiredService<IDispatchService>().ListWorkOrdersWithoutVisitsAsync(), commit: false);
        var made = await kit.Harness.RunAsync(services => services.GetRequiredService<IDispatchService>().CreateMissingAsync("dispatcher"));
        var again = await kit.Harness.RunAsync(services => services.GetRequiredService<IDispatchService>().CreateMissingAsync("dispatcher"));

        Assert.Equal([workOrder.ItemId], missing);
        Assert.Equal(1, made);
        Assert.Equal(0, again);
        Assert.Single(await kit.VisitsOfAsync(workOrder.ItemId));
    }

    [Fact]
    public async Task TwoDispatchers_AssigningOneTechnicianToOverlappingVisits_OneWins()
    {
        // Arrange
        await using var kit = await DispatchKit.CreateAsync(realLocks: true, travelDelay: TimeSpan.FromMilliseconds(300));
        var ana = await kit.AddTechnicianAsync("Ana Field");

        await kit.PublishShiftsAsync(ana);

        // Both are away from the depot, so each check waits for a travel time while the other dispatcher works.
        var (_, first) = await kit.AddJobAsync("No heat", DispatchKit.South);
        var (_, second) = await kit.AddJobAsync("Leaking valve", DispatchKit.South);

        // Act
        var results = await Task.WhenAll(
            kit.AssignAsync(first, DispatchKit.Monday(10), ana),
            kit.AssignAsync(second, DispatchKit.Monday(10, 30), ana));

        // Assert
        Assert.Single(results, result => result.Succeeded);

        var loser = Assert.Single(results, result => !result.Succeeded);
        var winner = results.Single(result => result.Succeeded).Visit;

        Assert.Contains($"Ana Field has {winner.Number}", Assert.Single(loser.Errors));

        var events = await kit.JobEventsAsync();
        var job = Assert.Single(events);

        Assert.Equal(ana.ItemId, DispatchKit.WorkerOf(job));
        Assert.Equal(winner.ItemId, job.SourceId);
        Assert.Equal(DispatchConstants.Source, job.Source);
    }

    [Fact]
    public async Task Candidates_ExcludeAnExpiredCertification()
    {
        // Arrange
        await using var kit = await DispatchKit.CreateAsync();
        var gas = await kit.Harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<CrestApps.OrchardCore.Skills.Services.ICertificationManager>();
            var certification = await manager.NewAsync();

            certification.Name = "Gas safety";
            await manager.CreateAsync(certification);

            return certification;
        });
        var ana = await kit.AddTechnicianAsync("Ana Field");
        var ben = await kit.AddTechnicianAsync("Ben Field");

        await kit.PublishShiftsAsync(ana, ben);
        await SaveCertificateAsync(kit, ana, gas.ItemId, new DateOnly(2026, 1, 1), new DateOnly(2027, 12, 31));
        await SaveCertificateAsync(kit, ben, gas.ItemId, new DateOnly(2024, 1, 1), new DateOnly(2026, 10, 31));
        await kit.UpdateTypeAsync(type => type.RequiredCertificationIds = [gas.ItemId]);

        var (_, visit) = await kit.AddJobAsync("Gas boiler", DispatchKit.Depot);

        // Act
        var found = await kit.CandidatesAsync(visit);

        // Assert
        var candidate = Assert.Single(found.Candidates);

        Assert.Equal(ana.ItemId, candidate.WorkerId);

        var excluded = Assert.Single(found.Excluded);

        Assert.Equal(ben.ItemId, excluded.WorkerId);
        Assert.Equal("Gas safety isn't valid on 9 Nov 2026.", excluded.Reason);
    }

    [Fact]
    public async Task ASkillThatNeedsACertificate_SaysSo_WhenTheCertificateHasExpired()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var gas = await kit.Harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<CrestApps.OrchardCore.Skills.Services.ICertificationManager>();
            var certification = await manager.NewAsync();

            certification.Name = "Gas safety";
            await manager.CreateAsync(certification);

            return certification;
        });
        var boilers = await kit.Harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<CrestApps.OrchardCore.Skills.Services.ISkillManager>();
            var skill = await manager.NewAsync();

            skill.Name = "Boilers";
            skill.RequiredCertificationId = gas.ItemId;
            await manager.CreateAsync(skill);

            return skill;
        });
        var ana = await kit.AddTechnicianAsync("Ana Field");

        await kit.PublishShiftsAsync(ana);
        await PlanningKit.GrantSkillAsync(kit.Harness, ana.ItemId, boilers.ItemId);
        await SaveCertificateAsync(kit, ana, gas.ItemId, new DateOnly(2024, 1, 1), new DateOnly(2026, 10, 31));

        var (_, visit) = await kit.AddJobAsync("Boiler down", DispatchKit.Depot, workOrder => workOrder.RequiredSkills = [new RequiredSkill { SkillId = boilers.ItemId }]);
        var found = await kit.CandidatesAsync(visit);

        Assert.Empty(found.Candidates);
        Assert.Equal("Boilers needs Gas safety, which isn't valid on 9 Nov 2026.", Assert.Single(found.Excluded).Reason);
    }

    [Fact]
    public async Task Candidates_NeedTheSkill_AShift_NoTimeOff_AndRoomForTheTripFromTheVisitBefore()
    {
        // Arrange
        await using var kit = await DispatchKit.CreateAsync();
        var boilers = await PlanningKit.AddSkillAsync(kit.Harness, "Boilers");
        var ana = await kit.AddTechnicianAsync("Ana Field");
        var ben = await kit.AddTechnicianAsync("Ben Field");
        var cara = await kit.AddTechnicianAsync("Cara Field");
        var dan = await kit.AddTechnicianAsync("Dan Field");
        var eve = await kit.AddTechnicianAsync("Eve Field");

        await kit.PublishShiftsAsync(ana, ben, cara, eve);

        foreach (var worker in new[] { ana, ben, cara, dan })
        {
            await PlanningKit.GrantSkillAsync(kit.Harness, worker.ItemId, boilers.ItemId);
        }

        await AddTimeOffAsync(kit, cara);

        // Ana is busy at the depot from 8 to 9, so she can reach the customer 20 minutes away at 9:30 with the buffer.
        var (_, early) = await kit.AddJobAsync("Depot check", DispatchKit.Depot);

        Assert.True((await kit.AssignAsync(early, DispatchKit.Monday(8), ana)).Succeeded);

        var (_, visit) = await kit.AddJobAsync("Boiler down", DispatchKit.South, workOrder => workOrder.RequiredSkills = [new RequiredSkill { SkillId = boilers.ItemId }]);

        // Act
        var found = await kit.CandidatesAsync(visit);

        // Assert
        var reasons = found.Excluded.ToDictionary(exclusion => exclusion.Name, exclusion => exclusion.Reason);

        Assert.Equal("Is on time off then.", reasons["Cara Field"]);
        Assert.Equal("Has no shift then.", reasons["Dan Field"]);
        Assert.Equal("Doesn't have Boilers at level 1.", reasons["Eve Field"]);

        var byName = found.Candidates.ToDictionary(candidate => candidate.Name);

        Assert.Equal(2, byName.Count);

        // Ben starts from the depot at 8 and travels 20 minutes; Ana leaves the depot at 9:00 plus the 10 minute buffer.
        Assert.Equal(DispatchKit.Monday(8, 20), byName["Ben Field"].StartUtc);
        Assert.Equal(20, byName["Ben Field"].TravelMinutes);
        Assert.Equal(DispatchKit.Monday(9, 30), byName["Ana Field"].StartUtc);
        Assert.Equal("Ben Field", found.Candidates[0].Name);
        Assert.True(byName["Ben Field"].ScoreParts[DefaultDispatchCandidateFinder.BalancePart] > byName["Ana Field"].ScoreParts[DefaultDispatchCandidateFinder.BalancePart]);
    }

    [Fact]
    public async Task Assigning_RefusesTimeOffShiftGapsAndTooLittleTravel()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");

        await kit.PublishShiftsAsync(ana);

        var (_, depot) = await kit.AddJobAsync("Depot check", DispatchKit.Depot);
        var (_, south) = await kit.AddJobAsync("Boiler down", DispatchKit.South);

        Assert.True((await kit.AssignAsync(depot, DispatchKit.Monday(9), ana)).Succeeded);

        // 10:00 leaves no time for the 10 minute buffer and the 20 minute trip.
        var tooSoon = await kit.AssignAsync(south, DispatchKit.Monday(10, 15), ana);
        var afterHours = await kit.AssignAsync(south, DispatchKit.Monday(16), ana);
        var inTime = await kit.AssignAsync(south, DispatchKit.Monday(10, 30), ana);

        Assert.Equal("Ana Field can't get there in time after V-000001, which ends at Mon 9 Nov 10:00.", Assert.Single(tooSoon.Errors));
        Assert.Equal("Ana Field isn't on shift then, or is on a break.", Assert.Single(afterHours.Errors));
        Assert.True(inTime.Succeeded, string.Join(" ", inTime.Errors));
        Assert.Equal(20, inTime.Visit.TravelMinutes);
    }

    [Fact]
    public async Task Assigning_KeepsAJobEventPerWorker_SchedulesTheWorkOrder_AndUnassigningUndoesBoth()
    {
        // Arrange
        await using var kit = await DispatchKit.CreateAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");
        var ben = await kit.AddTechnicianAsync("Ben Field");

        await kit.PublishShiftsAsync(ana, ben);

        var (workOrder, visit) = await kit.AddJobAsync("No heat", DispatchKit.Depot);

        // Act
        var assigned = await kit.AssignAsync(visit, DispatchKit.Monday(10), ana, ben);
        var scheduled = await kit.WorkOrderAsync(workOrder.ItemId);
        var bothEvents = await kit.JobEventsAsync();
        var moved = await kit.AssignAsync(visit, DispatchKit.Monday(13), ben);
        var movedEvents = await kit.JobEventsAsync();
        var unassigned = await kit.Harness.RunAsync(services => services.GetRequiredService<IDispatchService>().UnassignAsync(visit.ItemId, "dispatcher"));
        var ready = await kit.WorkOrderAsync(workOrder.ItemId);
        var noEvents = await kit.JobEventsAsync();

        // Assert
        Assert.True(assigned.Succeeded, string.Join(" ", assigned.Errors));
        Assert.Equal(VisitStatus.Scheduled, assigned.Visit.Status);
        Assert.Equal(ana.ItemId, assigned.Visit.LeadWorkerId);
        Assert.Equal(DispatchKit.Monday(9), assigned.Visit.ArrivalStartUtc);
        Assert.Equal(DispatchKit.Monday(11), assigned.Visit.ArrivalEndUtc);
        Assert.Equal(WorkOrderStatus.Scheduled, scheduled.Status);
        Assert.Equal(new[] { ana.ItemId, ben.ItemId }.Order(StringComparer.Ordinal), bothEvents.Select(DispatchKit.WorkerOf).Order(StringComparer.Ordinal));
        Assert.All(bothEvents, calendarEvent => Assert.Equal(DispatchKit.Monday(10), calendarEvent.StartUtc));

        Assert.True(moved.Succeeded, string.Join(" ", moved.Errors));

        var movedEvent = Assert.Single(movedEvents);

        Assert.Equal(ben.ItemId, DispatchKit.WorkerOf(movedEvent));
        Assert.Equal(DispatchKit.Monday(13), movedEvent.StartUtc);
        Assert.Equal(CalendarTransparency.Busy, movedEvent.Transparency);

        Assert.True(unassigned.Succeeded, string.Join(" ", unassigned.Errors));
        Assert.Equal(VisitStatus.Unscheduled, unassigned.Visit.Status);
        Assert.Empty(noEvents);
        Assert.Equal(WorkOrderStatus.Ready, ready.Status);
        Assert.Single(await kit.VisitsOfAsync(workOrder.ItemId));
    }

    [Fact]
    public async Task TheFieldStatuses_StartTheWorkOrder_AndAnIncompleteVisitMakesAReturnVisit()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");

        await kit.PublishShiftsAsync(ana);

        var (workOrder, visit) = await kit.AddJobAsync("No heat", DispatchKit.Depot);

        Assert.True((await kit.AssignAsync(visit, DispatchKit.Monday(10), ana)).Succeeded);
        Assert.True((await kit.MoveAsync(visit, VisitStatus.EnRoute)).Succeeded);
        Assert.Equal(WorkOrderStatus.InProgress, (await kit.WorkOrderAsync(workOrder.ItemId)).Status);
        Assert.True((await kit.MoveAsync(visit, VisitStatus.OnSite)).Succeeded);

        var noReason = await kit.MoveAsync(visit, VisitStatus.Incomplete);
        var incomplete = await kit.MoveAsync(visit, VisitStatus.Incomplete, "Needs a new heat exchanger.");
        var visits = await kit.VisitsOfAsync(workOrder.ItemId);

        Assert.Equal("Say why the visit couldn't be finished.", Assert.Single(noReason.Errors));
        Assert.True(incomplete.Succeeded);
        Assert.Equal(2, visits.Count);

        var returnVisit = visits.Single(item => item.ItemId != visit.ItemId);

        Assert.Equal(returnVisit.ItemId, incomplete.Visit.ReturnVisitId);
        Assert.Equal(VisitStatus.Unscheduled, returnVisit.Status);

        // The finished visit still shows on Ana's calendar; it no longer keeps her busy for others.
        Assert.Single(await kit.JobEventsAsync());
        Assert.Equal(["Unscheduled", "Scheduled", "EnRoute", "OnSite", "Incomplete"], incomplete.Visit.History.Select(change => change.Status.ToString()));
    }

    [Fact]
    public async Task CancelingTheWorkOrder_CancelsItsVisits_AndFreesTheWorker()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");

        await kit.PublishShiftsAsync(ana);

        var (workOrder, visit) = await kit.AddJobAsync("No heat", DispatchKit.Depot);

        Assert.True((await kit.AssignAsync(visit, DispatchKit.Monday(10), ana)).Succeeded);

        await kit.Harness.RunAsync(async services =>
        {
            var result = await services.GetRequiredService<WorkOrderService>().ChangeStatusAsync(workOrder.ItemId, WorkOrderStatus.Canceled, null, "The customer fixed it.", "dispatcher");

            Assert.Empty(result.Errors);
        });

        var canceled = Assert.Single(await kit.VisitsOfAsync(workOrder.ItemId));
        var (_, other) = await kit.AddJobAsync("Leaking valve", DispatchKit.Depot);

        Assert.Equal(VisitStatus.Canceled, canceled.Status);
        Assert.Equal("The customer fixed it.", canceled.History[^1].Note);
        Assert.Empty(await kit.JobEventsAsync());
        Assert.True((await kit.AssignAsync(other, DispatchKit.Monday(10), ana)).Succeeded);
    }

    private static Task SaveCertificateAsync(DispatchKit kit, WorkerProfile worker, string certificationId, DateOnly issuedOn, DateOnly expiresOn)
        => kit.Harness.RunAsync(async services =>
        {
            var result = await services.GetRequiredService<CrestApps.OrchardCore.Skills.Services.ICertificationRecordService>().SaveAsync(new CertificationRecord
            {
                SubjectKind = SkillSubjectKind.Worker,
                SubjectId = worker.ItemId,
                SubjectName = worker.DisplayName,
                CertificationId = certificationId,
                IssuedOn = issuedOn,
                ExpiresOn = expiresOn,
                Status = CertificationStatus.Valid,
            }, "tester");

            Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        });

    // Approved time off shows as a TimeOff event on the team's calendar.
    private static async Task AddTimeOffAsync(DispatchKit kit, WorkerProfile worker)
    {
        var calendarId = await kit.Harness.RunAsync(async services => (await services.GetRequiredService<CrestApps.OrchardCore.Workforce.Services.ITeamManager>().FindByIdAsync(kit.Team.ItemId)).CalendarId, commit: false);
        var created = await kit.Harness.RunAsync(async services =>
        {
            var calendars = services.GetRequiredService<CrestApps.OrchardCore.Calendars.Services.ICalendarService>();
            var events = services.GetRequiredService<CrestApps.OrchardCore.Calendars.Services.ICalendarEventManager>();
            var calendar = await calendars.FindByIdAsync(calendarId);
            var calendarEvent = await events.NewAsync(calendar, WorkforceConstants.EventTypes.TimeOff);

            calendarEvent.Title = worker.DisplayName;
            calendarEvent.Source = WorkforceConstants.Source;
            calendarEvent.SourceId = "request-1";
            calendarEvent.AllDay = false;
            calendarEvent.TimeZoneId = PlanningKit.Zone;
            calendarEvent.Start = PlanningKit.Monday.ToDateTime(new TimeOnly(0, 0));
            calendarEvent.End = PlanningKit.Monday.ToDateTime(new TimeOnly(23, 59));
            calendarEvent.Put(new TimeOffEventData { RequestId = "request-1", WorkerId = worker.ItemId });

            return await events.CreateAsync(calendarEvent, new CrestApps.OrchardCore.Calendars.Services.CalendarChangeOptions { SkipConflictCheck = true });
        });

        Assert.True(created.Succeeded, string.Join(" ", created.Errors.Select(error => error.Message)));
    }
}
