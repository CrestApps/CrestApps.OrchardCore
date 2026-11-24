using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Dispatch.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using YesSql;

namespace CrestApps.OrchardCore.Dispatch.AI;

/// <summary>
/// Lists visits with the Visits page's filters: status (or every open one), day, technician and a search.
/// </summary>
internal sealed class ListVisitsTool : DispatchAdminToolBase
{
    public const string ToolName = "listVisits";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "status": { "type": "string", "enum": ["open", "all", "Unscheduled", "Scheduled", "Dispatched", "EnRoute", "OnSite", "Paused", "Completed", "Incomplete", "Canceled"], "description": "Optional. 'open' (the default) for visits still to happen or happening, 'all' for every visit, or one status." },
        "search": { "type": "string", "description": "Optional. Text the visit number, the work order number or the summary contains." },
        "day": { "type": "string", "description": "Optional. A day (yyyy-MM-dd, in the dispatch time zone) the visits fall on." },
        "technician": { "type": "string", "description": "Optional. The technician's name or ID; only visits given to them." },
        "page": { "type": "integer", "minimum": 1, "description": "Optional. The page, from 1." },
        "pageSize": { "type": "integer", "minimum": 1, "maximum": 50, "description": "Optional. Visits per page, 20 by default, at most 50." }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Lists visits filtered by status (or all open), day, technician and a search on visit number, work order number or summary, with paging; also says how many work orders have no visit.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => DispatchPermissions.ViewDispatch;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var status = DispatchAdminTools.Read(arguments, "status") ?? VisitListQuery.OpenFilter;

        if (!status.Equals(VisitListQuery.OpenFilter, StringComparison.OrdinalIgnoreCase) && !status.Equals(VisitListQuery.AllFilter, StringComparison.OrdinalIgnoreCase))
        {
            if (!DispatchAdminTools.TryReadEnum<VisitStatus>(arguments, "status", out var parsed))
            {
                return $"'{status}' is not a visit status. Use open, all, or one of: {string.Join(", ", Enum.GetNames<VisitStatus>())}.";
            }

            status = parsed.ToString();
        }
        else
        {
            status = status.Equals(VisitListQuery.OpenFilter, StringComparison.OrdinalIgnoreCase) ? VisitListQuery.OpenFilter : VisitListQuery.AllFilter;
        }

        DateOnly? day = null;

        if (DispatchAdminTools.Has(arguments, "day"))
        {
            day = DispatchAdminTools.ReadDate(arguments, "day");

            if (day is null)
            {
                return "Give the day as yyyy-MM-dd.";
            }
        }

        string workerId = null;

        if (DispatchAdminTools.Read(arguments, "technician") is { } technician)
        {
            var names = await DispatchAdminTools.WorkerNamesAsync(services, cancellationToken);

            if (names.ContainsKey(technician))
            {
                workerId = technician;
            }
            else
            {
                var worker = await FieldServiceLookups.WorkerAsync(services, technician, cancellationToken);

                if (worker is null)
                {
                    return $"No technician is called '{technician}'.";
                }

                workerId = worker.ItemId;
            }
        }

        var zone = await DispatchAdminTools.TimeZoneAsync(services);
        var (page, pageSize) = DispatchAdminTools.ReadPaging(arguments);
        var filter = new VisitListFilter
        {
            Status = status,
            Search = DispatchAdminTools.Read(arguments, "search"),
            Day = day,
            TimeZoneId = zone,
            WorkerId = workerId,
        };
        var (total, visits) = await VisitListQuery.PageAsync(services.GetRequiredService<ISession>(), filter, (page - 1) * pageSize, pageSize, cancellationToken);
        var workerNames = await DispatchAdminTools.WorkerNamesAsync(services, cancellationToken);
        var items = visits.Select(visit => DispatchAdminTools.Row(visit, workerNames, zone)).ToList();
        var withoutVisits = (await services.GetRequiredService<IDispatchService>().ListWorkOrdersWithoutVisitsAsync(cancellationToken)).Count;

        return DispatchAdminTools.Json(new
        {
            items,
            total,
            page,
            pageSize,
            truncated = ((page - 1) * pageSize) + items.Count < total,
            workOrdersWithoutVisits = withoutVisits,
            timeZone = zone,
        });
    }
}
