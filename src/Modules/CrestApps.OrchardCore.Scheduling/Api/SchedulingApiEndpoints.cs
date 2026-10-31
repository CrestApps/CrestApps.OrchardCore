using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Scheduling.Workflows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore;

namespace CrestApps.OrchardCore.Scheduling.Api;

/// <summary>
/// The scheduling HTTP API under <c>/api/scheduling/v1</c>. Reading types and open times needs View appointments; booking,
/// moving and cancelling need Manage appointments. Every call goes through the same availability and booking rules as the
/// admin screens.
/// </summary>
internal static class SchedulingApiEndpoints
{
    public const string RoutePrefix = "api/scheduling/v1";

    public static IEndpointRouteBuilder MapSchedulingApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup(RoutePrefix)
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = OrchardCoreConstants.AuthenticationSchemes.Api })
            .DisableAntiforgery()
            .WithTags("Scheduling");

        group.MapGet("types", ListTypesAsync).WithName("SchedulingApiTypes").WithSummary("Lists the appointment types.");
        group.MapGet("types/{id}/slots", ListSlotsAsync).WithName("SchedulingApiSlots").WithSummary("Lists the open times of an appointment type.");
        group.MapPost("appointments", BookAsync).WithName("SchedulingApiBook").WithSummary("Books an appointment.");
        group.MapGet("appointments/{id}", GetAsync).WithName("SchedulingApiGet").WithSummary("Gets an appointment.");
        group.MapPost("appointments/{id}/confirm", ConfirmAsync).WithName("SchedulingApiConfirm").WithSummary("Confirms an appointment.");
        group.MapPost("appointments/{id}/reschedule", RescheduleAsync).WithName("SchedulingApiReschedule").WithSummary("Moves an appointment.");
        group.MapPost("appointments/{id}/cancel", CancelAsync).WithName("SchedulingApiCancel").WithSummary("Cancels an appointment.");

        return routes;
    }

    private static async Task<IResult> ListTypesAsync(HttpContext httpContext, IAuthorizationService authorizationService, AppointmentTypeStore typeStore)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, SchedulingPermissions.ViewAppointments))
        {
            return Results.Forbid();
        }

        return Results.Ok((await typeStore.ListAsync()).Select(type => new
        {
            id = type.ItemId,
            name = type.Name,
            description = type.Description,
            durationMinutes = type.DurationMinutes,
            capacity = type.Capacity,
            requiresConfirmation = type.RequiresConfirmation,
            enabled = type.Enabled,
        }));
    }

    private static async Task<IResult> ListSlotsAsync(string id, DateTimeOffset from, DateTimeOffset to, string resourceId, HttpContext httpContext, IAuthorizationService authorizationService, AppointmentTypeStore typeStore, ISchedulingAvailabilityService availabilityService)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, SchedulingPermissions.ViewAppointments))
        {
            return Results.Forbid();
        }

        var type = await typeStore.FindByIdAsync(id);

        if (type is null)
        {
            return Results.NotFound();
        }

        var slots = await availabilityService.FindSlotsAsync(type, from.UtcDateTime, to > from.AddDays(31) ? from.UtcDateTime.AddDays(31) : to.UtcDateTime, resourceId);

        return Results.Ok(slots.Select(slot => new
        {
            startUtc = AppointmentWorkflowInput.Format(slot.StartUtc),
            endUtc = AppointmentWorkflowInput.Format(slot.EndUtc),
            resourceIds = slot.ResourceIds,
            remaining = slot.Remaining,
        }));
    }

    private static async Task<IResult> BookAsync(AppointmentRequest request, HttpContext httpContext, IAuthorizationService authorizationService, IAppointmentService appointmentService)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, SchedulingPermissions.ManageAppointments))
        {
            return Results.Forbid();
        }

        request.Source = "Api";
        request.Actor = httpContext.User.Identity?.Name;

        return ToResult(await appointmentService.BookAsync(request), created: true);
    }

    private static async Task<IResult> GetAsync(string id, HttpContext httpContext, IAuthorizationService authorizationService, IAppointmentService appointmentService)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, SchedulingPermissions.ViewAppointments))
        {
            return Results.Forbid();
        }

        var appointment = await appointmentService.FindByIdAsync(id);

        return appointment is null ? Results.NotFound() : Results.Ok(AppointmentWorkflowInput.ToDictionary(appointment, null));
    }

    private static Task<IResult> ConfirmAsync(string id, HttpContext httpContext, IAuthorizationService authorizationService, IAppointmentService appointmentService)
    {
        return ChangeAsync(id, httpContext, authorizationService, appointmentService, appointment => appointmentService.ConfirmAsync(appointment, httpContext.User.Identity?.Name));
    }

    private static Task<IResult> RescheduleAsync(string id, AppointmentRescheduleInput input, HttpContext httpContext, IAuthorizationService authorizationService, IAppointmentService appointmentService)
    {
        return ChangeAsync(id, httpContext, authorizationService, appointmentService, appointment => appointmentService.RescheduleAsync(appointment, input.StartUtc.UtcDateTime, input.ResourceId, httpContext.User.Identity?.Name));
    }

    private static Task<IResult> CancelAsync(string id, AppointmentCancelInput input, HttpContext httpContext, IAuthorizationService authorizationService, IAppointmentService appointmentService)
    {
        return ChangeAsync(id, httpContext, authorizationService, appointmentService, appointment => appointmentService.CancelAsync(appointment, input?.Reason, httpContext.User.Identity?.Name));
    }

    private static async Task<IResult> ChangeAsync(string id, HttpContext httpContext, IAuthorizationService authorizationService, IAppointmentService appointmentService, Func<Appointment, Task<AppointmentResult>> change)
    {
        if (!await authorizationService.AuthorizeAsync(httpContext.User, SchedulingPermissions.ManageAppointments))
        {
            return Results.Forbid();
        }

        var appointment = await appointmentService.FindByIdAsync(id);

        return appointment is null ? Results.NotFound() : ToResult(await change(appointment), created: false);
    }

    private static IResult ToResult(AppointmentResult result, bool created)
    {
        if (result.Succeeded)
        {
            var body = AppointmentWorkflowInput.ToDictionary(result.Appointment, null);

            return created ? Results.Created($"/{RoutePrefix}/appointments/{result.Appointment.ItemId}", body) : Results.Ok(body);
        }

        return result.Conflict
            ? Results.Problem(string.Join(" ", result.Errors), statusCode: StatusCodes.Status409Conflict, title: "Conflict")
            : Results.ValidationProblem(new Dictionary<string, string[]> { [string.Empty] = result.Errors.ToArray() });
    }
}
