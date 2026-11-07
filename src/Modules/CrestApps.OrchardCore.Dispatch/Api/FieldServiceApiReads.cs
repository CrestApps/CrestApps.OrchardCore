using System.Globalization;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Dispatch.Controllers;
using CrestApps.OrchardCore.Dispatch.Core.Services;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Dispatch.Api;

/// <summary>
/// The field service API's reads of checklists, the dispatch board and service agreements.
/// </summary>
internal static partial class FieldServiceApiEndpoints
{
    /// <summary>
    /// Maps the service agreement reads at <c>api/work-orders/v1/agreements</c>, when Service Agreements is on.
    /// </summary>
    /// <param name="routes">The routes.</param>
    /// <returns>The routes.</returns>
    public static IEndpointRouteBuilder MapServiceAgreementsApi(this IEndpointRouteBuilder routes)
    {
        var agreements = Group(routes, WorkOrdersPrefix, "Work Orders");

        agreements.MapGet("agreements", (Delegate)ListAgreementsAsync).WithSummary("Lists service agreements by status and text, highest number first.");
        agreements.MapGet("agreements/{id}", (Delegate)GetAgreementAsync).WithSummary("Gets a service agreement with its schedules.");

        return routes;
    }

    internal static async Task<IResult> ListChecklistsAsync(HttpContext httpContext)
    {
        if (await DeniedAsync(httpContext, WorkOrdersPermissions.ViewWorkOrders) is { } denied)
        {
            return denied;
        }

        var checklists = await httpContext.RequestServices.GetRequiredService<WorkOrderSettingManager<ChecklistTemplate>>().GetAllAsync();

        return Json(checklists.OrderBy(checklist => checklist.Name, StringComparer.CurrentCultureIgnoreCase).Select(checklist => new
        {
            id = checklist.ItemId,
            name = checklist.Name,
            questions = checklist.Questions,
        }));
    }

    internal static async Task<IResult> BoardAsync(string day, string teamId, HttpContext httpContext)
    {
        if (await DeniedAsync(httpContext, DispatchPermissions.ViewDispatch) is { } denied)
        {
            return denied;
        }

        var services = httpContext.RequestServices;
        var resources = services.GetRequiredService<DispatchResources>();
        var dispatch = services.GetRequiredService<IDispatchService>();
        var zone = await services.GetRequiredService<DispatchTimeZone>().GetAsync();
        var now = services.GetRequiredService<IClock>().UtcNow;
        var date = DateOnly.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : DateOnly.FromDateTime(CalendarTime.ToLocal(now, zone));
        var fromUtc = CalendarTime.ToUtc(date.ToDateTime(TimeOnly.MinValue), zone);
        var toUtc = CalendarTime.ToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue), zone);
        var teamIds = string.IsNullOrEmpty(teamId) ? null : await services.GetRequiredService<ITeamManager>().GetSubtreeIdsAsync(teamId, httpContext.RequestAborted);
        var workers = (await resources.ListWorkersAsync(httpContext.RequestAborted))
            .Where(worker => teamIds is null || worker.MembershipsOn(date).Any(membership => teamIds.Contains(membership.TeamId)))
            .OrderBy(worker => worker.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var technicians = new List<object>();

        foreach (var worker in workers)
        {
            var availability = await resources.GetAvailabilityAsync(worker, fromUtc, toUtc, null, httpContext.RequestAborted);

            technicians.Add(new
            {
                id = worker.ItemId,
                name = worker.DisplayName,
                onShift = availability.HasShift,
                open = availability.Open.Select(span => new { startUtc = span.StartUtc, endUtc = span.EndUtc }),
                timeOff = availability.TimeOff.Select(span => new { startUtc = span.StartUtc, endUtc = span.EndUtc }),
            });
        }

        var scheduled = await dispatch.ListScheduledAsync(fromUtc, toUtc, teamIds is null ? null : [.. workers.Select(worker => worker.ItemId)], httpContext.RequestAborted);
        var waiting = await dispatch.ListUnscheduledAsync(MaxTake, httpContext.RequestAborted);

        return Json(new
        {
            day = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            timeZone = zone,
            fromUtc,
            toUtc,
            technicians,
            visits = scheduled.OrderBy(visit => visit.StartUtc).Select(VisitSummary),
            unscheduled = waiting.Select(VisitSummary),
        });
    }

    internal static async Task<IResult> ListAgreementsAsync(string status, string q, int? skip, int? take, HttpContext httpContext)
    {
        if (await DeniedAsync(httpContext, WorkOrdersPermissions.ManageServiceAgreements) is { } denied)
        {
            return denied;
        }

        var query = httpContext.RequestServices.GetRequiredService<ISession>().Query<ServiceAgreement, ServiceAgreementIndex>(collection: WorkOrdersConstants.CollectionName);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var value = status.Trim();

            query = query.Where(index => index.Status == value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var text = q.Trim();

            query = query.Where(index => index.Number.Contains(text) || index.Name.Contains(text));
        }

        var count = await query.CountAsync();
        var items = await query.OrderByDescending(index => index.Number).Skip(Math.Max(0, skip ?? 0)).Take(Math.Clamp(take ?? 50, 1, MaxTake)).ListAsync();

        return Json(new
        {
            count,
            items = items.Select(agreement => new
            {
                id = agreement.ItemId,
                number = agreement.Number,
                name = agreement.Name,
                status = agreement.Status,
                customerId = agreement.CustomerId,
                accountId = agreement.AccountId,
                startDate = agreement.StartDate,
                endDate = agreement.EndDate,
                schedules = agreement.Schedules.Count,
            }),
        });
    }

    internal static async Task<IResult> GetAgreementAsync(string id, HttpContext httpContext)
    {
        if (await DeniedAsync(httpContext, WorkOrdersPermissions.ManageServiceAgreements) is { } denied)
        {
            return denied;
        }

        var agreement = await httpContext.RequestServices.GetRequiredService<ServiceAgreementManager>().FindByIdAsync(id);

        return agreement is null ? Results.NotFound() : Json(agreement);
    }
}
