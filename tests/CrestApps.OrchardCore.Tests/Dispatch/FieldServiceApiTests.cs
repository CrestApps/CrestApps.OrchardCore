using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.Dispatch;
using CrestApps.OrchardCore.Dispatch.Api;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CrestApps.OrchardCore.Tests.Dispatch;

/// <summary>
/// The field service API: a work order made once per Idempotency-Key, visits assigned and moved along through it, the
/// admin's permissions on every call, and the day optimized.
/// </summary>
public sealed class FieldServiceApiTests
{
    private static readonly JsonSerializerOptions _web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task AWorkOrder_IsCreatedOncePerIdempotencyKey_AndGetsItsVisit()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var user = CalendarTestHarness.User("office", WorkOrdersPermissions.ManageWorkOrders.Name, DispatchPermissions.ViewDispatch.Name);
        var body = $$"""{ "typeId": "{{kit.Type.ItemId}}", "summary": "No heat", "priority": "High", "contact": { "name": "Pat", "phone": "+15550100" } }""";

        var first = await kit.Harness.RunAsync(services => FieldServiceApiEndpoints.CreateWorkOrderAsync(Context(services, user, body, "order-1")));
        var again = await kit.Harness.RunAsync(services => FieldServiceApiEndpoints.CreateWorkOrderAsync(Context(services, user, body, "order-1")));
        var other = await kit.Harness.RunAsync(services => FieldServiceApiEndpoints.CreateWorkOrderAsync(Context(services, user, body, "order-2")));
        var id = Value(first).GetProperty("itemId").GetString();
        var listed = await kit.Harness.RunAsync(services => FieldServiceApiEndpoints.ListWorkOrdersAsync(null, true, "No heat", null, null, Context(services, user)), commit: false);
        var visits = await kit.VisitsOfAsync(id);

        Assert.Equal(201, Status(first));
        Assert.Equal(id, Value(again).GetProperty("itemId").GetString());
        Assert.NotEqual(id, Value(other).GetProperty("itemId").GetString());
        Assert.Equal(2, Value(listed).GetProperty("count").GetInt32());
        Assert.Equal(WorkOrderPriority.High, (await kit.WorkOrderAsync(id)).Priority);
        Assert.Single(visits);
    }

    [Fact]
    public async Task AVisit_IsAssignedAndMovedAlong_WithTheDispatchersPermission()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");

        await kit.PublishShiftsAsync(ana);

        var (workOrder, visit) = await kit.AddJobAsync("No heat", DispatchKit.Depot);
        var viewer = CalendarTestHarness.User("viewer", DispatchPermissions.ViewDispatch.Name);
        var dispatcher = CalendarTestHarness.User("dispatcher", DispatchPermissions.ManageDispatch.Name);
        var assign = $$"""{ "workerIds": ["{{ana.ItemId}}"], "startUtc": "{{DispatchKit.Monday(10):O}}" }""";

        var refused = await kit.Harness.RunAsync(services => FieldServiceApiEndpoints.AssignAsync(visit.ItemId, Context(services, viewer, assign)));
        var assigned = await kit.Harness.RunAsync(services => FieldServiceApiEndpoints.AssignAsync(visit.ItemId, Context(services, dispatcher, assign)));
        var enRoute = await kit.Harness.RunAsync(services => FieldServiceApiEndpoints.ChangeVisitStatusAsync(visit.ItemId, Context(services, dispatcher, """{ "status": "EnRoute" }""")));
        var badMove = await kit.Harness.RunAsync(services => FieldServiceApiEndpoints.ChangeVisitStatusAsync(visit.ItemId, Context(services, dispatcher, """{ "status": "Completed" }""")));
        var listed = await kit.Harness.RunAsync(services => FieldServiceApiEndpoints.ListVisitsAsync(DispatchKit.Monday(0).ToString("O"), DispatchKit.Monday(23).ToString("O"), ana.ItemId, null, Context(services, viewer)), commit: false);

        Assert.Equal(403, Status(refused));
        Assert.Equal(200, Status(assigned));
        Assert.Equal(200, Status(enRoute));
        Assert.Equal(400, Status(badMove));
        Assert.Equal(VisitStatus.EnRoute, Assert.Single(await kit.VisitsOfAsync(workOrder.ItemId)).Status);
        Assert.Equal(visit.ItemId, Assert.Single(Value(listed).EnumerateArray()).GetProperty("id").GetString());
    }

    [Fact]
    public async Task TheDay_IsOptimizedThroughTheApi()
    {
        await using var kit = await DispatchKit.CreateAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");

        await kit.PublishShiftsAsync(ana);
        await kit.AddJobAsync("No heat", DispatchKit.Depot);

        var dispatcher = CalendarTestHarness.User("dispatcher", DispatchPermissions.ManageDispatch.Name);
        var proposal = await kit.Harness.RunAsync(services => FieldServiceApiEndpoints.OptimizeAsync(Context(services, dispatcher, """{ "day": "2026-11-09", "includeUnscheduled": true }""")), commit: false);
        var moves = Value(proposal).GetProperty("moves");

        Assert.Equal(200, Status(proposal));
        Assert.Equal(ana.ItemId, Assert.Single(moves.EnumerateArray()).GetProperty("toWorkerId").GetString());
    }

    private static DefaultHttpContext Context(IServiceProvider services, ClaimsPrincipal user, string body = null, string idempotencyKey = null)
    {
        var context = new DefaultHttpContext { User = user, RequestServices = services };

        if (body is not null)
        {
            context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
            context.Request.ContentType = "application/json";
        }

        if (idempotencyKey is not null)
        {
            context.Request.Headers["Idempotency-Key"] = idempotencyKey;
        }

        return context;
    }

    private static int Status(IResult result) => result switch
    {
        ForbidHttpResult => 403,

        // A JSON result without a status is a 200.
        IStatusCodeHttpResult status => status.StatusCode ?? 200,
        _ => 0,
    };

    private static JsonElement Value(IResult result)
        => JsonSerializer.SerializeToElement(((IValueHttpResult)result).Value, _web);
}
