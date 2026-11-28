using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Assets;
using CrestApps.OrchardCore.Assets.Core.Indexes;
using CrestApps.OrchardCore.Dispatch.Core;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using YesSql;

namespace CrestApps.OrchardCore.Dispatch.AI;

/// <summary>
/// Gets one visit as its admin page shows it, without the customer tracking link (a bearer link).
/// </summary>
internal sealed class GetVisitTool : DispatchAdminToolBase
{
    public const string ToolName = "getVisit";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "visit": { "type": "string", "description": "The visit's number (V-000012), its ID, or its work order's number (WO-000012) for the work order's open visit." }
      },
      "required": ["visit"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Gets one visit: its work order, status and history, technicians, times, arrival window, ETA, travel, equipment, whether it is pinned or at risk, the customer notices sent and the statuses it can move to next.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => DispatchPermissions.ViewDispatch;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var reference = DispatchAdminTools.Read(arguments, "visit");

        if (reference is null)
        {
            return "Give the visit's number or ID, or its work order's number.";
        }

        var visit = await DispatchAdminTools.FindVisitAsync(services, reference, cancellationToken);

        if (visit is null)
        {
            return $"No visit or work order is numbered '{reference}'. Use listVisits to find it.";
        }

        var zone = await DispatchAdminTools.TimeZoneAsync(services);
        var names = await DispatchAdminTools.WorkerNamesAsync(services, cancellationToken);
        var type = string.IsNullOrEmpty(visit.TypeId) ? null : await services.GetRequiredService<WorkOrderSettingManager<WorkOrderType>>().FindByIdAsync(visit.TypeId, cancellationToken);
        var session = services.GetRequiredService<ISession>();
        var equipment = new List<object>();

        foreach (var assetId in visit.EquipmentAssetIds)
        {
            var asset = await session.QueryIndex<AssetIndex>(index => index.ItemId == assetId, collection: AssetsConstants.CollectionName).FirstOrDefaultAsync(cancellationToken);

            equipment.Add(new { id = assetId, name = asset?.Name ?? assetId });
        }

        var others = (await services.GetRequiredService<IDispatchService>().ListForWorkOrderAsync(visit.WorkOrderId, cancellationToken))
            .Where(other => other.ItemId != visit.ItemId)
            .Select(other => new { id = other.ItemId, number = other.Number, status = other.Status.ToString(), start = DispatchAdminTools.Local(other.StartUtc, zone) })
            .ToList();

        return DispatchAdminTools.Json(new
        {
            id = visit.ItemId,
            number = visit.Number,
            workOrderId = visit.WorkOrderId,
            workOrderNumber = visit.WorkOrderNumber,
            summary = visit.Summary,
            type = type?.Name,
            priority = visit.Priority.ToString(),
            status = visit.Status.ToString(),
            nextStatuses = VisitStatusRules.Next(visit.Status).Select(status => status.ToString()).ToArray(),
            durationMinutes = visit.DurationMinutes,
            crewSize = visit.CrewSize,
            leadTechnician = visit.LeadWorkerId is null ? null : names.GetValueOrDefault(visit.LeadWorkerId, visit.LeadWorkerId),
            technicians = visit.WorkerIds.Select(id => new { id, name = names.GetValueOrDefault(id, id) }).ToArray(),
            start = DispatchAdminTools.Local(visit.StartUtc, zone),
            end = DispatchAdminTools.Local(visit.EndUtc, zone),
            arrivalWindow = visit.ArrivalStartUtc is null ? null : new { from = DispatchAdminTools.Local(visit.ArrivalStartUtc, zone), to = DispatchAdminTools.Local(visit.ArrivalEndUtc, zone) },
            eta = DispatchAdminTools.Local(visit.EtaUtc, zone),
            due = DispatchAdminTools.Local(visit.DueUtc, zone),
            travelMinutes = visit.TravelMinutes,
            travelKm = visit.TravelKm,
            equipment,
            pinned = visit.Pinned,
            atRisk = visit.AtRiskReason is null ? null : new { reason = visit.AtRiskReason, since = DispatchAdminTools.Local(visit.AtRiskSinceUtc, zone) },
            incompleteReason = visit.IncompleteReason,
            returnVisitId = visit.ReturnVisitId,
            otherVisits = others,
            notices = visit.Notices.Select(notice => new { kind = notice.Kind, channel = notice.Channel, sent = DispatchAdminTools.Local(notice.Utc, zone), error = notice.Error }).ToArray(),
            history = visit.History.Select(change => new { status = change.Status.ToString(), at = DispatchAdminTools.Local(change.Utc, zone), by = change.By, note = change.Note }).ToArray(),
            timeZone = zone,
        });
    }
}
