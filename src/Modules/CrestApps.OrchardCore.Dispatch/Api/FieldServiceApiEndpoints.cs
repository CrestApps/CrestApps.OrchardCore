using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Dispatch.Core;
using CrestApps.OrchardCore.Dispatch.Mobile;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.Security.Permissions;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Dispatch.Api;

/// <summary>
/// The field service HTTP API: work orders at <c>api/work-orders/v1</c>, visits and optimizing at
/// <c>api/dispatch/v1</c>, and geocoding and territories at <c>api/geo/v1</c>. Each endpoint checks the same
/// permission as the admin. Creating a work order takes an <c>Idempotency-Key</c> header, so sending it again returns
/// the work order made the first time.
/// </summary>
internal static partial class FieldServiceApiEndpoints
{
    public const string WorkOrdersPrefix = "api/work-orders/v1";

    public const string DispatchPrefix = "api/dispatch/v1";

    public const string GeoPrefix = "api/geo/v1";

    private const int MaxTake = 200;

    private const int MaxDays = 31;

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static IEndpointRouteBuilder MapFieldServiceApi(this IEndpointRouteBuilder routes)
    {
        var workOrders = Group(routes, WorkOrdersPrefix, "Work Orders");

        workOrders.MapGet("types", (Delegate)ListTypesAsync).WithSummary("Lists the work order types.");
        workOrders.MapGet("checklists", (Delegate)ListChecklistsAsync).WithSummary("Lists the checklist templates with their questions.");
        workOrders.MapGet("work-orders", (Delegate)ListWorkOrdersAsync).WithSummary("Lists work orders, newest first, by status, open or not, and text.");
        workOrders.MapGet("work-orders/{id}", (Delegate)GetWorkOrderAsync).WithSummary("Gets a work order with its lines, checklists, notes and signatures.");
        workOrders.MapPost("work-orders", (Delegate)CreateWorkOrderAsync).WithSummary("Creates a work order, once per Idempotency-Key.");
        workOrders.MapPost("work-orders/{id}/status", (Delegate)ChangeWorkOrderStatusAsync).WithSummary("Moves a work order to another status.");
        workOrders.MapPost("work-orders/{id}/notes", (Delegate)AddNoteAsync).WithSummary("Adds a note.");
        workOrders.MapPost("work-orders/{id}/answers", (Delegate)AnswerAsync).WithSummary("Answers a checklist question.");
        workOrders.MapPost("work-orders/{id}/parts", (Delegate)UsePartAsync).WithSummary("Records parts used from a line.");
        workOrders.MapPost("work-orders/{id}/signatures", (Delegate)SignAsync).WithSummary("Records who signed, with the signature image's path.");
        workOrders.MapPost("work-orders/{id}/attachments", (Delegate)AttachAsync).WithSummary("Adds a photo or document already in the media library.");
        workOrders.MapPost("work-orders/{id}/complete", (Delegate)CompleteAsync).WithSummary("Completes a work order once its answers, signature and photos are in.");

        var dispatch = Group(routes, DispatchPrefix, "Dispatch");

        dispatch.MapGet("board", (Delegate)BoardAsync).WithSummary("Gets a day's board: the technicians with their open time and time off, the visits scheduled, and the ones waiting.");
        dispatch.MapGet("visits", (Delegate)ListVisitsAsync).WithSummary("Lists visits scheduled between two times, or the ones waiting for a time.");
        dispatch.MapGet("visits/{id}", (Delegate)GetVisitAsync).WithSummary("Gets a visit with its history.");
        dispatch.MapGet("visits/{id}/candidates", (Delegate)CandidatesAsync).WithSummary("Lists the technicians who can do a visit on a day, best first, and why the others can't.");
        dispatch.MapPost("visits/{id}/assign", (Delegate)AssignAsync).WithSummary("Gives a visit its technicians and start.");
        dispatch.MapPost("visits/{id}/unassign", (Delegate)UnassignAsync).WithSummary("Takes a visit off its technicians.");
        dispatch.MapPost("visits/{id}/status", (Delegate)ChangeVisitStatusAsync).WithSummary("Moves a visit to another status.");
        dispatch.MapPost("optimize", (Delegate)OptimizeAsync).WithSummary("Proposes a better day: the moves, and the travel before and after.");
        dispatch.MapPost("optimize/apply", (Delegate)ApplyAsync).WithSummary("Applies the chosen moves of a proposal.");
        dispatch.MapGet("positions", (Delegate)PositionsAsync).WithSummary("Gets technicians' latest positions from the last 30 minutes, when they share them.");

        var geo = Group(routes, GeoPrefix, "Geo");

        geo.MapGet("geocode", (Delegate)GeocodeAsync).WithSummary("Finds the coordinates of an address.");
        geo.MapGet("territories/resolve", (Delegate)ResolveTerritoryAsync).WithSummary("Finds the territory a place or postal code is in.");

        return routes;
    }

    private static RouteGroupBuilder Group(IEndpointRouteBuilder routes, string prefix, string tag)
        => routes.MapGroup(prefix)
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = OrchardCoreConstants.AuthenticationSchemes.Api })
            .DisableAntiforgery()
            .WithTags(tag);

    internal static async Task<IResult> ListTypesAsync(HttpContext httpContext)
    {
        if (await DeniedAsync(httpContext, WorkOrdersPermissions.ViewWorkOrders) is { } denied)
        {
            return denied;
        }

        var types = await httpContext.RequestServices.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>().GetAllAsync();

        return Json(types.OrderBy(type => type.Name, StringComparer.CurrentCultureIgnoreCase).Select(type => new
        {
            id = type.ItemId,
            name = type.Name,
            durationMinutes = type.DefaultDurationMinutes,
            crewSize = type.CrewSize,
            requiresSignature = type.RequiresSignature,
            requiredPhotos = type.RequiredPhotos,
        }));
    }

    internal static async Task<IResult> ListWorkOrdersAsync(string status, bool? open, string q, int? skip, int? take, HttpContext httpContext)
    {
        if (await DeniedAsync(httpContext, WorkOrdersPermissions.ViewWorkOrders) is { } denied)
        {
            return denied;
        }

        var query = httpContext.RequestServices.GetRequiredService<ISession>().Query<WorkOrder, WorkOrderIndex>(collection: WorkOrdersConstants.CollectionName);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(index => index.Status == status.Trim());
        }

        if (open is { } isOpen)
        {
            query = query.Where(index => index.IsOpen == isOpen);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var text = q.Trim();

            query = query.Where(index => index.Number.Contains(text) || index.Summary.Contains(text));
        }

        var count = await query.CountAsync();
        var items = await query.OrderByDescending(index => index.CreatedUtc).Skip(Math.Max(0, skip ?? 0)).Take(Math.Clamp(take ?? 50, 1, MaxTake)).ListAsync();

        return Json(new { count, items = items.Select(Summary) });
    }

    internal static async Task<IResult> GetWorkOrderAsync(string id, HttpContext httpContext)
    {
        if (await DeniedAsync(httpContext, WorkOrdersPermissions.ViewWorkOrders) is { } denied)
        {
            return denied;
        }

        var workOrder = await httpContext.RequestServices.GetRequiredService<WorkOrderService>().FindAsync(id);

        return workOrder is null ? Results.NotFound() : Json(workOrder);
    }

    internal static async Task<IResult> CreateWorkOrderAsync(HttpContext httpContext)
    {
        if (await DeniedAsync(httpContext, WorkOrdersPermissions.ManageWorkOrders) is { } denied)
        {
            return denied;
        }

        var input = await ReadAsync<WorkOrderInput>(httpContext);

        if (input is null || string.IsNullOrWhiteSpace(input.TypeId))
        {
            return Problem("Give the work order's type (typeId).");
        }

        var services = httpContext.RequestServices;
        var session = services.GetRequiredService<ISession>();
        var key = httpContext.Request.Headers["Idempotency-Key"].ToString().Trim();
        var sourceId = string.IsNullOrEmpty(key) ? null : "api:" + (key.Length > 60 ? key[..60] : key);

        // The same key gives back the work order it made.
        if (sourceId is not null
            && await session.Query<WorkOrder, WorkOrderIndex>(index => index.Source == nameof(WorkOrderSource.Api) && index.SourceId == sourceId, collection: WorkOrdersConstants.CollectionName).FirstOrDefaultAsync() is { } existing)
        {
            return Json(existing);
        }

        var workOrders = services.GetRequiredService<WorkOrderService>();
        var workOrder = await workOrders.NewAsync(input.TypeId, httpContext.RequestAborted);

        if (workOrder is null)
        {
            return Problem("The type doesn't exist.");
        }

        workOrder.Summary = input.Summary;
        workOrder.Description = input.Description;
        workOrder.Priority = input.Priority ?? workOrder.Priority;
        workOrder.CustomerId = input.CustomerId;
        workOrder.AccountId = input.AccountId;
        workOrder.ContactId = input.ContactId;
        workOrder.Contact = input.Contact ?? workOrder.Contact;
        workOrder.ServiceAddress = input.ServiceAddress ?? workOrder.ServiceAddress;
        workOrder.AccessNotes = input.AccessNotes;
        workOrder.AssetIds = input.AssetIds ?? workOrder.AssetIds;
        workOrder.EarliestUtc = input.EarliestUtc;
        workOrder.LatestUtc = input.LatestUtc;
        workOrder.Source = WorkOrderSource.Api;
        workOrder.SourceId = sourceId;

        var result = await workOrders.CreateAsync(workOrder, Actor(httpContext), httpContext.RequestAborted);

        if (result.Errors.Count > 0)
        {
            return Problem(string.Join(" ", result.Errors));
        }

        await session.SaveChangesAsync(httpContext.RequestAborted);

        return Results.Json(result.WorkOrder, JsonOptions, statusCode: StatusCodes.Status201Created);
    }

    internal static Task<IResult> ChangeWorkOrderStatusAsync(string id, HttpContext httpContext)
        => WorkOrderChangeAsync<WorkOrderStatusInput>(httpContext, (service, input, actor, token) => service.ChangeStatusAsync(id, input.Status, input.SubStatusId, input.Note, actor, token));

    internal static Task<IResult> AddNoteAsync(string id, HttpContext httpContext)
        => WorkOrderChangeAsync<TextInput>(httpContext, (service, input, actor, token) => service.AddNoteAsync(id, input.Text, actor, token));

    internal static Task<IResult> AnswerAsync(string id, HttpContext httpContext)
        => WorkOrderChangeAsync<AnswerInput>(httpContext, (service, input, actor, token) => service.AnswerAsync(id, input.ChecklistIndex, input.QuestionId, input.Value, actor, input.AnsweredUtc, token));

    internal static Task<IResult> UsePartAsync(string id, HttpContext httpContext)
        => WorkOrderChangeAsync<PartInput>(httpContext, (service, input, actor, token) => service.UsePartAsync(id, input.LineId, input.Quantity, input.LocationId, actor, token));

    internal static Task<IResult> SignAsync(string id, HttpContext httpContext)
        => WorkOrderChangeAsync<SignatureInput>(httpContext, (service, input, actor, token) => service.SignAsync(id, input.Name, input.ImagePath, actor, token));

    internal static Task<IResult> AttachAsync(string id, HttpContext httpContext)
        => WorkOrderChangeAsync<AttachmentInput>(httpContext, (service, input, actor, token) => service.AttachAsync(id, input.Path, actor, token));

    internal static Task<IResult> CompleteAsync(string id, HttpContext httpContext)
        => WorkOrderChangeAsync<object>(httpContext, (service, _, actor, token) => service.CompleteAsync(id, actor, token), readBody: false);

    internal static async Task<IResult> ListVisitsAsync(string from, string to, string workerId, bool? unscheduled, HttpContext httpContext)
    {
        if (await DeniedAsync(httpContext, DispatchPermissions.ViewDispatch) is { } denied)
        {
            return denied;
        }

        var dispatch = httpContext.RequestServices.GetRequiredService<IDispatchService>();

        if (unscheduled == true)
        {
            return Json((await dispatch.ListUnscheduledAsync(MaxTake, httpContext.RequestAborted)).Select(VisitSummary));
        }

        if (!TryTime(from, out var fromUtc) || !TryTime(to, out var toUtc) || toUtc <= fromUtc || (toUtc - fromUtc).TotalDays > MaxDays)
        {
            return Problem($"Give from and to as UTC times, at most {MaxDays} days apart, or unscheduled=true.");
        }

        var visits = await dispatch.ListScheduledAsync(fromUtc, toUtc, string.IsNullOrEmpty(workerId) ? null : [workerId], httpContext.RequestAborted);

        return Json(visits.OrderBy(visit => visit.StartUtc).Select(VisitSummary));
    }

    internal static async Task<IResult> GetVisitAsync(string id, HttpContext httpContext)
    {
        if (await DeniedAsync(httpContext, DispatchPermissions.ViewDispatch) is { } denied)
        {
            return denied;
        }

        var visit = await httpContext.RequestServices.GetRequiredService<IDispatchService>().FindAsync(id, httpContext.RequestAborted);

        return visit is null ? Results.NotFound() : Json(visit);
    }

    internal static async Task<IResult> CandidatesAsync(string id, string from, string to, HttpContext httpContext)
    {
        if (await DeniedAsync(httpContext, DispatchPermissions.ViewDispatch) is { } denied)
        {
            return denied;
        }

        var services = httpContext.RequestServices;
        var visit = await services.GetRequiredService<IDispatchService>().FindAsync(id, httpContext.RequestAborted);

        if (visit is null)
        {
            return Results.NotFound();
        }

        if (!TryTime(from, out var fromUtc) || !TryTime(to, out var toUtc) || toUtc <= fromUtc || (toUtc - fromUtc).TotalDays > MaxDays)
        {
            return Problem($"Give from and to as UTC times, at most {MaxDays} days apart.");
        }

        var result = await services.GetRequiredService<IDispatchCandidateFinder>().FindAsync(visit, fromUtc, toUtc, cancellationToken: httpContext.RequestAborted);

        return Json(result);
    }

    internal static Task<IResult> AssignAsync(string id, HttpContext httpContext)
        => VisitChangeAsync<VisitAssignment>(httpContext, (service, input, actor, token) => service.AssignAsync(id, input, actor, token));

    internal static Task<IResult> UnassignAsync(string id, HttpContext httpContext)
        => VisitChangeAsync<object>(httpContext, (service, _, actor, token) => service.UnassignAsync(id, actor, token), readBody: false);

    internal static Task<IResult> ChangeVisitStatusAsync(string id, HttpContext httpContext)
        => VisitChangeAsync<VisitStatusInput>(httpContext, (service, input, actor, token) => service.ChangeStatusAsync(id, input.Status, input.Note, actor, token));

    internal static async Task<IResult> OptimizeAsync(HttpContext httpContext)
    {
        if (await DeniedAsync(httpContext, DispatchPermissions.ManageDispatch) is { } denied)
        {
            return denied;
        }

        var input = await ReadAsync<OptimizeInput>(httpContext);

        // No technicians given means every one.
        if (input is null || !DateOnly.TryParseExact(input.Day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            return Problem("Give the day as yyyy-MM-dd.");
        }

        var proposal = await httpContext.RequestServices.GetRequiredService<IDispatchOptimizationService>().ProposeAsync(day, input.WorkerIds is { Count: > 0 } workerIds ? workerIds : null, input.IncludeUnscheduled, httpContext.RequestAborted);

        return Json(proposal);
    }

    internal static async Task<IResult> ApplyAsync(HttpContext httpContext)
    {
        if (await DeniedAsync(httpContext, DispatchPermissions.ManageDispatch) is { } denied)
        {
            return denied;
        }

        var input = await ReadAsync<ApplyInput>(httpContext);

        if (input?.Moves is null)
        {
            return Problem("Give the moves to apply.");
        }

        var services = httpContext.RequestServices;
        var result = await services.GetRequiredService<IDispatchOptimizationService>().ApplyAsync(input.Moves, Actor(httpContext), httpContext.RequestAborted);

        await services.GetRequiredService<ISession>().SaveChangesAsync(httpContext.RequestAborted);

        return Json(result);
    }

    internal static async Task<IResult> PositionsAsync(HttpContext httpContext)
    {
        if (await DeniedAsync(httpContext, DispatchPermissions.ViewDispatch) is { } denied)
        {
            return denied;
        }

        var services = httpContext.RequestServices;
        var locations = services.GetService<IWorkerLocationService>();

        if (locations is null)
        {
            return Json(Array.Empty<object>());
        }

        var workers = await services.GetRequiredService<Core.Services.DispatchResources>().ListWorkersAsync(httpContext.RequestAborted);
        var since = services.GetRequiredService<global::OrchardCore.Modules.IClock>().UtcNow.AddMinutes(-30);
        var names = workers.ToDictionary(worker => worker.ItemId, worker => worker.DisplayName, StringComparer.Ordinal);
        var positions = await locations.LatestAsync(names.Keys, since, httpContext.RequestAborted);

        return Json(positions.Select(ping => new { workerId = ping.WorkerId, name = names.GetValueOrDefault(ping.WorkerId), utc = ping.Utc, latitude = ping.Latitude, longitude = ping.Longitude, visitId = ping.VisitId }));
    }

    internal static async Task<IResult> GeocodeAsync(string address, HttpContext httpContext)
    {
        if (await DeniedAsync(httpContext, DispatchPermissions.ViewDispatch) is { } denied)
        {
            return denied;
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            return Problem("Give the address.");
        }

        var result = await httpContext.RequestServices.GetRequiredService<IGeoService>().GeocodeAsync(new GeocodeQuery { Text = address.Trim() }, httpContext.RequestAborted);

        return Json(result);
    }

    internal static async Task<IResult> ResolveTerritoryAsync(double? latitude, double? longitude, string postalCode, HttpContext httpContext)
    {
        if (await DeniedAsync(httpContext, DispatchPermissions.ViewDispatch) is { } denied)
        {
            return denied;
        }

        GeoPoint? point = latitude is { } lat && longitude is { } lng ? new GeoPoint(lat, lng) : null;

        if (point is null && string.IsNullOrWhiteSpace(postalCode))
        {
            return Problem("Give a latitude and longitude, or a postal code.");
        }

        var match = await httpContext.RequestServices.GetRequiredService<ITerritoryResolver>().ResolveAsync(point, postalCode?.Trim(), httpContext.RequestAborted);

        return match?.Territory is not { } territory ? Results.NotFound() : Json(new { id = territory.ItemId, name = territory.Name, timeZoneId = territory.TimeZoneId, matchedBy = match.MatchedBy });
    }

    private static async Task<IResult> WorkOrderChangeAsync<TInput>(HttpContext httpContext, Func<WorkOrderService, TInput, string, CancellationToken, Task<WorkOrderResult>> change, bool readBody = true)
        where TInput : class
    {
        if (await DeniedAsync(httpContext, WorkOrdersPermissions.ManageWorkOrders) is { } denied)
        {
            return denied;
        }

        var input = readBody ? await ReadAsync<TInput>(httpContext) : null;

        if (readBody && input is null)
        {
            return Problem("The body can't be read.");
        }

        var services = httpContext.RequestServices;
        var result = await change(services.GetRequiredService<WorkOrderService>(), input, Actor(httpContext), httpContext.RequestAborted);

        if (result.Errors.Count > 0)
        {
            return Problem(string.Join(" ", result.Errors));
        }

        await services.GetRequiredService<ISession>().SaveChangesAsync(httpContext.RequestAborted);

        return Json(result.WorkOrder);
    }

    private static async Task<IResult> VisitChangeAsync<TInput>(HttpContext httpContext, Func<IDispatchService, TInput, string, CancellationToken, Task<DispatchResult>> change, bool readBody = true)
        where TInput : class
    {
        if (await DeniedAsync(httpContext, DispatchPermissions.ManageDispatch) is { } denied)
        {
            return denied;
        }

        var input = readBody ? await ReadAsync<TInput>(httpContext) : null;

        if (readBody && input is null)
        {
            return Problem("The body can't be read.");
        }

        var services = httpContext.RequestServices;
        var result = await change(services.GetRequiredService<IDispatchService>(), input, Actor(httpContext), httpContext.RequestAborted);

        if (result.Errors.Count > 0)
        {
            return Problem(string.Join(" ", result.Errors));
        }

        await services.GetRequiredService<ISession>().SaveChangesAsync(httpContext.RequestAborted);

        return Json(result.Visit);
    }

    private static async Task<IResult> DeniedAsync(HttpContext httpContext, Permission permission)
        => await httpContext.RequestServices.GetRequiredService<IAuthorizationService>().AuthorizeAsync(httpContext.User, permission) ? null : Results.Forbid();

    private static object Summary(WorkOrder workOrder) => new
    {
        id = workOrder.ItemId,
        number = workOrder.Number,
        typeId = workOrder.TypeId,
        status = workOrder.Status,
        priority = workOrder.Priority,
        summary = workOrder.Summary,
        customerId = workOrder.CustomerId,
        territoryId = workOrder.TerritoryId,
        respondByUtc = workOrder.Sla?.RespondByUtc,
        resolveByUtc = workOrder.Sla?.ResolveByUtc,
        createdUtc = workOrder.CreatedUtc,
    };

    private static object VisitSummary(ServiceVisit visit) => new
    {
        id = visit.ItemId,
        number = visit.Number,
        workOrderId = visit.WorkOrderId,
        workOrderNumber = visit.WorkOrderNumber,
        summary = visit.Summary,
        status = visit.Status,
        startUtc = visit.StartUtc,
        endUtc = visit.EndUtc,
        arrivalStartUtc = visit.ArrivalStartUtc,
        arrivalEndUtc = visit.ArrivalEndUtc,
        workerIds = visit.WorkerIds,
        etaUtc = visit.EtaUtc,
        atRiskReason = visit.AtRiskReason,
    };

    private static IResult Json(object value) => Results.Json(value, JsonOptions);

    private static IResult Problem(string detail)
        => Results.Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest);

    private static string Actor(HttpContext httpContext) => httpContext.User.Identity?.Name ?? "API";

    private static bool TryTime(string value, out DateTime utc)
    {
        var parsed = DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out utc);

        utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

        return parsed;
    }

    private static async Task<T> ReadAsync<T>(HttpContext httpContext)
        where T : class
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(httpContext.Request.Body, JsonOptions, httpContext.RequestAborted);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// A work order to create through the API.
/// </summary>
internal sealed class WorkOrderInput
{
    public string TypeId { get; set; }

    public string Summary { get; set; }

    public string Description { get; set; }

    public WorkOrderPriority? Priority { get; set; }

    public string CustomerId { get; set; }

    public string AccountId { get; set; }

    public string ContactId { get; set; }

    public WorkOrderContact Contact { get; set; }

    public Addresses.Models.Address ServiceAddress { get; set; }

    public string AccessNotes { get; set; }

    public List<string> AssetIds { get; set; }

    public DateTime? EarliestUtc { get; set; }

    public DateTime? LatestUtc { get; set; }
}

internal sealed class WorkOrderStatusInput
{
    public WorkOrderStatus Status { get; set; }

    public string SubStatusId { get; set; }

    public string Note { get; set; }
}

internal sealed class TextInput
{
    public string Text { get; set; }
}

internal sealed class AnswerInput
{
    public int ChecklistIndex { get; set; }

    public string QuestionId { get; set; }

    public string Value { get; set; }

    public DateTime? AnsweredUtc { get; set; }
}

internal sealed class PartInput
{
    public string LineId { get; set; }

    public decimal Quantity { get; set; }

    public string LocationId { get; set; }
}

internal sealed class SignatureInput
{
    public string Name { get; set; }

    public string ImagePath { get; set; }
}

internal sealed class AttachmentInput
{
    public string Path { get; set; }
}

internal sealed class VisitStatusInput
{
    public VisitStatus Status { get; set; }

    public string Note { get; set; }
}

internal sealed class OptimizeInput
{
    public string Day { get; set; }

    public List<string> WorkerIds { get; set; }

    public bool IncludeUnscheduled { get; set; }
}

internal sealed class ApplyInput
{
    public List<ProposalMove> Moves { get; set; }
}
