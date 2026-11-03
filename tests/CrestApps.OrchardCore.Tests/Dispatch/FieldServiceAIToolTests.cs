using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Dispatch;
using CrestApps.OrchardCore.Dispatch.AI;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Dispatch;

/// <summary>
/// The field service AI tools: run as the signed-in user with the admin's permissions, except the customer's "where is
/// my technician", which answers only for the caller's own phone number.
/// </summary>
public sealed class FieldServiceAIToolTests
{
    [Fact]
    public async Task WorkOrderCreate_NeedsThePermission_AndGivesTheNumber()
    {
        await using var kit = await DispatchKit.CreateAsync();

        var refused = await InvokeAsync<WorkOrderCreateTool>(kit, CalendarTestHarness.User("viewer", WorkOrdersPermissions.ViewWorkOrders.Name), new() { ["summary"] = "No heat" });
        var created = await InvokeAsync<WorkOrderCreateTool>(kit, CalendarTestHarness.User("agent", WorkOrdersPermissions.ManageWorkOrders.Name), new()
        {
            ["summary"] = "No heat",
            ["priority"] = "Urgent",
            ["contactName"] = "Pat Customer",
            ["contactPhone"] = "(555) 010-0123",
            ["street"] = "1 Main St",
            ["city"] = "Boston",
        });
        var number = JsonDocument.Parse(created).RootElement.GetProperty("number").GetString();
        var status = await InvokeAsync<WorkOrderStatusTool>(kit, CalendarTestHarness.User("agent", WorkOrdersPermissions.ViewWorkOrders.Name), new() { ["number"] = number });

        Assert.Equal("You can't create work orders.", refused);
        Assert.StartsWith("WO-", number, StringComparison.Ordinal);
        Assert.Equal("Urgent", JsonDocument.Parse(status).RootElement.GetProperty("priority").GetString());
        Assert.Single(JsonDocument.Parse(status).RootElement.GetProperty("visits").EnumerateArray());
    }

    [Fact]
    public async Task TheDispatchAssistant_FindsATechnician_AndReschedulesTheVisit()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");

        await kit.PublishShiftsAsync(ana);

        var (workOrder, visit) = await kit.AddJobAsync("No heat", DispatchKit.Depot);
        var dispatcher = CalendarTestHarness.User("dispatcher", DispatchPermissions.ManageDispatch.Name);

        var candidates = await InvokeAsync<DispatchFindTechnicianTool>(kit, dispatcher, new() { ["number"] = workOrder.Number, ["date"] = "2026-11-09" });
        var moved = await InvokeAsync<DispatchRescheduleVisitTool>(kit, dispatcher, new() { ["number"] = visit.Number, ["technician"] = "ana field", ["start"] = "2026-11-09T10:00" });
        var tooLate = await InvokeAsync<DispatchRescheduleVisitTool>(kit, dispatcher, new() { ["number"] = visit.Number, ["start"] = "2026-11-09T21:00" });
        var after = Assert.Single(await kit.VisitsOfAsync(workOrder.ItemId));

        Assert.Equal("Ana Field", JsonDocument.Parse(candidates).RootElement.GetProperty("candidates")[0].GetProperty("name").GetString());
        Assert.Equal("2026-11-09T10:00", JsonDocument.Parse(moved).RootElement.GetProperty("start").GetString());
        Assert.True(JsonDocument.Parse(tooLate).RootElement.TryGetProperty("refused", out _));
        Assert.Equal(DispatchKit.Monday(10), after.StartUtc);
        Assert.Equal([ana.ItemId], after.WorkerIds);
    }

    [Fact]
    public async Task OptimizeAndExplain_Proposes_ThenAppliesOnlyTheApprovedMoves()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");

        await kit.PublishShiftsAsync(ana);

        var (workOrder, visit) = await kit.AddJobAsync("No heat", DispatchKit.Depot);
        var dispatcher = CalendarTestHarness.User("dispatcher", DispatchPermissions.ManageDispatch.Name);

        var proposal = await InvokeAsync<DispatchOptimizeTool>(kit, dispatcher, new() { ["date"] = "2026-11-09", ["includeUnscheduled"] = true });
        var untouched = Assert.Single(await kit.VisitsOfAsync(workOrder.ItemId));
        var applied = await InvokeAsync<DispatchOptimizeTool>(kit, dispatcher, new() { ["date"] = "2026-11-09", ["includeUnscheduled"] = true, ["apply"] = new[] { visit.Number } });

        Assert.Equal("Ana Field", JsonDocument.Parse(proposal).RootElement.GetProperty("moves")[0].GetProperty("to").GetString());
        Assert.Equal(VisitStatus.Unscheduled, untouched.Status);
        Assert.Equal([visit.ItemId], JsonDocument.Parse(applied).RootElement.GetProperty("applied").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(VisitStatus.Scheduled, Assert.Single(await kit.VisitsOfAsync(workOrder.ItemId)).Status);
    }

    [Fact]
    public async Task WhereIsMyTechnician_AnswersOnlyForTheCallersPhone()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");

        await kit.PublishShiftsAsync(ana);

        var (_, visit) = await kit.AddJobAsync("No heat", DispatchKit.Depot, order => order.Contact = new WorkOrderContact { Name = "Pat", Phone = "+1 (555) 010-0123" });

        Assert.True((await kit.AssignAsync(visit, DispatchKit.Monday(10), ana)).Succeeded);

        var caller = await InvokeAsync<WhereIsMyTechnicianTool>(kit, new ClaimsPrincipal(), new() { ["phone"] = "5550100123" });
        var stranger = await InvokeAsync<WhereIsMyTechnicianTool>(kit, new ClaimsPrincipal(), new() { ["phone"] = "5550199999" });
        var found = JsonDocument.Parse(caller).RootElement.GetProperty("visits")[0];

        Assert.Equal("Ana", found.GetProperty("technician").GetString());
        Assert.Equal("Scheduled", found.GetProperty("status").GetString());
        Assert.False(found.TryGetProperty("address", out _));
        Assert.StartsWith("No upcoming visit", stranger, StringComparison.Ordinal);
    }

    private static Task<string> InvokeAsync<TTool>(DispatchKit kit, ClaimsPrincipal user, Dictionary<string, object> arguments)
        where TTool : AIFunction
        => kit.Harness.RunAsync(async services =>
        {
            services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext { User = user, RequestServices = services };

            var tool = ActivatorUtilities.CreateInstance<TTool>(services);
            var result = await tool.InvokeAsync(new AIFunctionArguments(arguments.ToDictionary(pair => pair.Key, pair => (object)JsonSerializer.SerializeToElement(pair.Value))) { Services = services });

            return result?.ToString();
        });
}
