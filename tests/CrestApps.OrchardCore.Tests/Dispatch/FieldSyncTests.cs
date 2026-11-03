using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Dispatch;

/// <summary>
/// The technician app: commands made offline are applied once and in order when it reconnects, the schedule on the
/// server wins, and positions are kept only with consent and while working.
/// </summary>
public sealed class FieldSyncTests
{
    // A 1×1 PNG, as the signature canvas sends it.
    private const string Png = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    [Fact]
    public async Task TheOutbox_IsAppliedOnceAndInOrder_AndSendingItAgainChangesNothing()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");

        await kit.PublishShiftsAsync(ana);

        var (workOrder, visit) = await kit.AddJobAsync("No heat", DispatchKit.Depot);

        Assert.True((await kit.AssignAsync(visit, DispatchKit.Monday(10), ana)).Succeeded);

        // Made in a basement with no signal: on the way, there, a break for a part, back, and a note.
        FieldCommand[] outbox =
        [
            Status("k1", visit, VisitStatus.EnRoute),
            Status("k2", visit, VisitStatus.OnSite),
            Status("k3", visit, VisitStatus.Paused),
            Status("k4", visit, VisitStatus.OnSite),
            new() { Key = "k5", Kind = FieldCommandKind.Note, VisitId = visit.ItemId, Utc = DispatchKit.Monday(10, 40), Text = "Replaced the igniter." },
            new() { Key = "k5", Kind = FieldCommandKind.Note, VisitId = visit.ItemId, Utc = DispatchKit.Monday(10, 40), Text = "Replaced the igniter." },
        ];

        var first = await ApplyAsync(kit, ana, outbox);
        var again = await ApplyAsync(kit, ana, outbox);
        var after = Assert.Single(await kit.VisitsOfAsync(workOrder.ItemId));
        var notes = (await kit.WorkOrderAsync(workOrder.ItemId)).Notes.Count(note => note.Text == "Replaced the igniter.");

        Assert.Equal([FieldCommandOutcome.Applied, FieldCommandOutcome.Applied, FieldCommandOutcome.Applied, FieldCommandOutcome.Applied, FieldCommandOutcome.Applied, FieldCommandOutcome.Duplicate], first.Select(result => result.Outcome));
        Assert.All(again, result => Assert.Equal(FieldCommandOutcome.Duplicate, result.Outcome));
        Assert.Equal(VisitStatus.OnSite, after.Status);
        Assert.Equal(["Unscheduled", "Scheduled", "EnRoute", "OnSite", "Paused", "OnSite"], after.History.Select(change => change.Status.ToString()));
        Assert.Equal(1, notes);
    }

    [Fact]
    public async Task CommandsForAVisitGivenToSomeoneElse_AreRefusedWithTheReason()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");
        var ben = await kit.AddTechnicianAsync("Ben Field");

        await kit.PublishShiftsAsync(ana, ben);

        var (workOrder, visit) = await kit.AddJobAsync("No heat", DispatchKit.Depot);

        Assert.True((await kit.AssignAsync(visit, DispatchKit.Monday(10), ana)).Succeeded);

        // While Ana was offline, the dispatcher gave the visit to Ben.
        Assert.True((await kit.AssignAsync(visit, DispatchKit.Monday(11), ben)).Succeeded);

        var results = await ApplyAsync(kit, ana, Status("k1", visit, VisitStatus.EnRoute), new FieldCommand { Key = "k2", Kind = FieldCommandKind.Note, VisitId = visit.ItemId, Utc = DispatchKit.Monday(9), Text = "On my way." });
        var after = Assert.Single(await kit.VisitsOfAsync(workOrder.ItemId));

        Assert.All(results, result =>
        {
            Assert.Equal(FieldCommandOutcome.Rejected, result.Outcome);
            Assert.Equal(FieldRejections.Moved, result.Code);
            Assert.Equal($"{visit.Number} was given to someone else.", result.Reason);
        });
        Assert.Equal(VisitStatus.Scheduled, after.Status);
        Assert.DoesNotContain((await kit.WorkOrderAsync(workOrder.ItemId)).Notes, note => note.Text == "On my way.");

        // Ben's own commands go through.
        Assert.Equal(FieldCommandOutcome.Applied, Assert.Single(await ApplyAsync(kit, ben, Status("k3", visit, VisitStatus.EnRoute))).Outcome);
    }

    [Fact]
    public async Task FinishingTheVisit_WaitsForTheSignature_ThenCompletesTheWorkOrderToo()
    {
        await using var kit = await DispatchKit.CreateAsync();

        await kit.UpdateTypeAsync(type => type.RequiresSignature = true);

        var ana = await kit.AddTechnicianAsync("Ana Field");

        await kit.PublishShiftsAsync(ana);

        var (workOrder, visit) = await kit.AddJobAsync("No heat", DispatchKit.Depot);

        Assert.True((await kit.AssignAsync(visit, DispatchKit.Monday(10), ana)).Succeeded);

        var unsigned = await ApplyAsync(kit, ana, Status("k1", visit, VisitStatus.OnSite), Status("k2", visit, VisitStatus.Completed));
        var signed = await ApplyAsync(
            kit,
            ana,
            new FieldCommand { Key = "k3", Kind = FieldCommandKind.Signature, VisitId = visit.ItemId, Utc = DispatchKit.Monday(11), Name = "Pat Customer", Data = Png },
            new FieldCommand { Key = "k4", Kind = FieldCommandKind.Photo, VisitId = visit.ItemId, Utc = DispatchKit.Monday(11), Data = Png },
            Status("k5", visit, VisitStatus.Completed));
        var completed = await kit.WorkOrderAsync(workOrder.ItemId);

        Assert.Equal(FieldCommandOutcome.Applied, unsigned[0].Outcome);
        Assert.Equal(FieldCommandOutcome.Rejected, unsigned[1].Outcome);
        Assert.Equal("The customer must sign.", unsigned[1].Reason);
        Assert.All(signed, result => Assert.Equal(FieldCommandOutcome.Applied, result.Outcome));
        Assert.Equal(WorkOrderStatus.Completed, completed.Status);
        Assert.Equal(VisitStatus.Completed, Assert.Single(await kit.VisitsOfAsync(workOrder.ItemId)).Status);

        var signature = Assert.Single(completed.Signatures);

        Assert.Equal("Pat Customer", signature.Name);
        Assert.Equal($"work-orders/{workOrder.Number}/signature-k3.png", signature.ImagePath);
        Assert.Equal([$"work-orders/{workOrder.Number}/photo-k4.png"], completed.Attachments);
        Assert.Equal(2, kit.Files.Files.Count);
    }

    [Fact]
    public async Task AnAnswerGivenAfterTheAppsOne_IsKept()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");

        await kit.PublishShiftsAsync(ana);

        var (workOrder, visit) = await kit.AddJobAsync("No heat", DispatchKit.Depot, order => order.Checklists.Add(new WorkOrderChecklist
        {
            Name = "Safety",
            Questions = [new ChecklistQuestion { Id = "pressure", Text = "Gas pressure in range?", Kind = ChecklistQuestionKind.YesNo }],
        }));

        Assert.True((await kit.AssignAsync(visit, DispatchKit.Monday(10), ana)).Succeeded);

        // The office answered at 10:30; the app's answer from 10:20 arrives later and loses.
        kit.Now = DispatchKit.Monday(10, 30);
        await kit.Harness.RunAsync(services => services.GetRequiredService<WorkOrderService>().AnswerAsync(workOrder.ItemId, 0, "pressure", "No", "office"));
        kit.Now = DispatchKit.Monday(10, 45);

        var older = Assert.Single(await ApplyAsync(kit, ana, Answer("k1", visit, "Yes", DispatchKit.Monday(10, 20))));
        var kept = (await kit.WorkOrderAsync(workOrder.ItemId)).Checklists[0].Answers.Single().Value;
        var newer = Assert.Single(await ApplyAsync(kit, ana, Answer("k2", visit, "Yes", DispatchKit.Monday(10, 40))));

        Assert.Equal(FieldRejections.Stale, older.Code);
        Assert.Equal("\"Gas pressure in range?\" was answered again after this, so the later answer is kept.", older.Reason);
        Assert.Equal("No", kept);
        Assert.Equal(FieldCommandOutcome.Applied, newer.Outcome);
        Assert.Equal("Yes", (await kit.WorkOrderAsync(workOrder.ItemId)).Checklists[0].Answers.Single().Value);
    }

    [Fact]
    public async Task TwoAnswersFromTheOutbox_ToOneQuestion_KeepTheLaterOne()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");

        await kit.PublishShiftsAsync(ana);

        var (workOrder, visit) = await kit.AddJobAsync("No heat", DispatchKit.Depot, order => order.Checklists.Add(new WorkOrderChecklist
        {
            Name = "Safety",
            Questions = [new ChecklistQuestion { Id = "pressure", Text = "Gas pressure in range?", Kind = ChecklistQuestionKind.YesNo }],
        }));

        Assert.True((await kit.AssignAsync(visit, DispatchKit.Monday(10), ana)).Succeeded);

        // Answered offline at 10:20 and corrected at 10:21; the sync comes at 11:00. The answer keeps the device's time.
        kit.Now = DispatchKit.Monday(11);

        var results = await ApplyAsync(kit, ana, Answer("k1", visit, "Yes", DispatchKit.Monday(10, 20)), Answer("k2", visit, "No", DispatchKit.Monday(10, 21)));
        var answer = (await kit.WorkOrderAsync(workOrder.ItemId)).Checklists[0].Answers.Single();

        Assert.All(results, result => Assert.Equal(FieldCommandOutcome.Applied, result.Outcome));
        Assert.Equal("No", answer.Value);
        Assert.Equal(DispatchKit.Monday(10, 21), answer.Utc);
    }

    [Fact]
    public async Task Positions_AreKeptOnlyWithConsentAndWhileWorking_AndRefreshTheEta()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");
        var ben = await kit.AddTechnicianAsync("Ben Field");

        await kit.PublishShiftsAsync(ana, ben);

        var (workOrder, visit) = await kit.AddJobAsync("No heat", DispatchKit.Depot);

        kit.Now = DispatchKit.Monday(9, 30);
        Assert.True((await kit.AssignAsync(visit, DispatchKit.Monday(10), ana)).Succeeded);

        var withoutConsent = await PingAsync(kit, ana, DispatchKit.Monday(9, 30));

        await SetConsentAsync(kit, ana, true);
        await SetConsentAsync(kit, ben, true);

        var noVisit = await PingAsync(kit, ben, DispatchKit.Monday(9, 30));

        Assert.True((await kit.MoveAsync(visit, VisitStatus.EnRoute)).Succeeded);

        // 20 km south of the job, which is 20 minutes away.
        kit.Now = DispatchKit.Monday(9, 40);

        var onTheWay = await PingAsync(kit, ana, DispatchKit.Monday(9, 40));
        var latest = await LatestAsync(kit, ana);

        Assert.Equal(0, withoutConsent.Kept);
        Assert.Equal("Location sharing is off.", withoutConsent.Reason);
        Assert.Equal("You have no visit now, so your location isn't kept.", noVisit.Reason);
        Assert.Equal(1, onTheWay.Kept);
        Assert.Equal(DispatchKit.Monday(10), Assert.Single(await kit.VisitsOfAsync(workOrder.ItemId)).EtaUtc);
        Assert.Equal(DispatchKit.South.Latitude, Assert.Single(latest).Latitude);

        // Withdrawing takes the positions with it.
        await SetConsentAsync(kit, ana, false);

        Assert.Empty(await LatestAsync(kit, ana));
        Assert.Equal("Location sharing is off.", (await PingAsync(kit, ana, DispatchKit.Monday(9, 45))).Reason);
    }

    [Fact]
    public async Task OldPositions_AreRemovedAfterTheirRetention()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");

        await kit.PublishShiftsAsync(ana);

        var (_, visit) = await kit.AddJobAsync("No heat", DispatchKit.Depot);

        kit.Now = DispatchKit.Monday(9, 30);
        Assert.True((await kit.AssignAsync(visit, DispatchKit.Monday(10), ana)).Succeeded);
        await SetConsentAsync(kit, ana, true);
        Assert.Equal(1, (await PingAsync(kit, ana, DispatchKit.Monday(9, 30))).Kept);

        kit.Now = DispatchKit.Monday(9, 30).AddDays(29);
        Assert.Equal(0, await kit.Harness.RunAsync(services => services.GetRequiredService<IWorkerLocationService>().PruneAsync()));

        kit.Now = DispatchKit.Monday(9, 30).AddDays(31);
        Assert.Equal(1, await kit.Harness.RunAsync(services => services.GetRequiredService<IWorkerLocationService>().PruneAsync()));
        Assert.Empty(await LatestAsync(kit, ana));
    }

    [Fact]
    public async Task ThePack_HasTheDaysVisits_WithTheirWorkOrders_AndWhatTheTechnicianCanDoNext()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");

        await kit.PublishShiftsAsync(ana);

        var (workOrder, visit) = await kit.AddJobAsync("No heat", DispatchKit.Depot, order => order.Contact = new WorkOrderContact { Name = "Pat Customer", Phone = "+15550100" });

        kit.Now = DispatchKit.Monday(7);
        Assert.True((await kit.AssignAsync(visit, DispatchKit.Monday(10), ana)).Succeeded);

        var pack = await kit.Harness.RunAsync(services => services.GetRequiredService<IFieldSyncService>().GetPackAsync(ana.ItemId), commit: false);
        var field = Assert.Single(pack.Visits);

        Assert.Equal("Ana Field", pack.WorkerName);
        Assert.Equal(visit.ItemId, field.Id);
        Assert.True(field.IsLead);
        Assert.Equal(DispatchKit.Monday(10), field.StartUtc);
        Assert.Equal(workOrder.Number, field.WorkOrder.Number);
        Assert.Equal("No heat", field.WorkOrder.Summary);
        Assert.Equal("+15550100", field.WorkOrder.Contact.Phone);
        Assert.Equal(DispatchKit.Depot.Latitude, field.WorkOrder.Latitude);
        Assert.Equal(["EnRoute", "OnSite"], pack.NextStatuses["Scheduled"]);
        Assert.Equal(["Paused", "Completed", "Incomplete"], pack.NextStatuses["OnSite"]);
        Assert.Empty(pack.NextStatuses["Completed"]);
        Assert.False(pack.Location.Consented);
        Assert.Equal(60, pack.Location.IntervalSeconds);

        // A week before, the visit is too far ahead to keep on the device.
        kit.Now = DispatchKit.Monday(7).AddDays(-7);
        Assert.Empty((await kit.Harness.RunAsync(services => services.GetRequiredService<IFieldSyncService>().GetPackAsync(ana.ItemId), commit: false)).Visits);
    }

    private static FieldCommand Status(string key, ServiceVisit visit, VisitStatus status)
        => new() { Key = key, Kind = FieldCommandKind.Status, VisitId = visit.ItemId, Utc = DispatchKit.Monday(10), Status = status };

    private static FieldCommand Answer(string key, ServiceVisit visit, string value, DateTime utc)
        => new() { Key = key, Kind = FieldCommandKind.Answer, VisitId = visit.ItemId, Utc = utc, ChecklistIndex = 0, QuestionId = "pressure", Value = value };

    private static Task<IReadOnlyList<FieldCommandResult>> ApplyAsync(DispatchKit kit, WorkerProfile worker, params FieldCommand[] commands)
        => kit.Harness.RunAsync(services => services.GetRequiredService<IFieldSyncService>().ApplyAsync(worker.ItemId, commands, worker.DisplayName));

    private static Task<WorkerLocationConsent> SetConsentAsync(DispatchKit kit, WorkerProfile worker, bool given)
        => kit.Harness.RunAsync(services => services.GetRequiredService<IWorkerLocationService>().SetConsentAsync(worker.ItemId, given));

    private static Task<LocationPingResult> PingAsync(DispatchKit kit, WorkerProfile worker, DateTime utc)
        => kit.Harness.RunAsync(services => services.GetRequiredService<IWorkerLocationService>().RecordAsync(worker.ItemId, [new LocationPingInput { Utc = utc, Latitude = DispatchKit.South.Latitude, Longitude = DispatchKit.South.Longitude, AccuracyMeters = 12 }]));

    private static Task<IReadOnlyList<WorkerLocationPing>> LatestAsync(DispatchKit kit, WorkerProfile worker)
        => kit.Harness.RunAsync(services => services.GetRequiredService<IWorkerLocationService>().LatestAsync([worker.ItemId], DispatchKit.Monday(0)), commit: false);
}
