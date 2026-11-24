using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using YesSql;

namespace CrestApps.OrchardCore.WorkOrders.AI;

/// <summary>
/// Lists work orders as the Work Orders page does: newest first, by status (or every open one), type, priority, asset
/// and a search on the number or summary.
/// </summary>
internal sealed class ListWorkOrdersTool : WorkOrderToolBase
{
    public const string ToolName = "listWorkOrders";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "status": { "type": "string", "enum": ["open", "all", "New", "Ready", "Scheduled", "InProgress", "OnHold", "Completed", "Closed", "Canceled"], "description": "Which work orders: open (the default) for every one still to finish, all, or one status." },
        "typeId": { "type": "string", "description": "Only work orders of this work order type ID (from listWorkOrderSettings with kind types)." },
        "priority": { "type": "string", "enum": ["Low", "Normal", "High", "Urgent"], "description": "Only work orders of this priority." },
        "assetId": { "type": "string", "description": "Only work orders for this asset ID." },
        "search": { "type": "string", "description": "Text the work order's number or summary contains." },
        "page": { "type": "integer", "minimum": 1, "description": "The page to return, from 1." },
        "pageSize": { "type": "integer", "minimum": 1, "maximum": 50, "description": "How many work orders per page, at most 50. Defaults to 20." }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Lists work orders, newest first, filtered by status (or all open), type, priority, asset and a search on number or summary, with paging. Use it to find a work order's number.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkOrdersPermissions.ViewWorkOrders;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var statusText = WorkOrderToolSupport.Read(arguments, "status") ?? WorkOrderListQuery.OpenFilter;
        string status;

        if (string.Equals(statusText, WorkOrderListQuery.OpenFilter, StringComparison.OrdinalIgnoreCase))
        {
            status = WorkOrderListQuery.OpenFilter;
        }
        else if (string.Equals(statusText, WorkOrderListQuery.AllFilter, StringComparison.OrdinalIgnoreCase))
        {
            status = WorkOrderListQuery.AllFilter;
        }
        else if (WorkOrderToolSupport.TryParseEnum<WorkOrderStatus>(statusText, out var parsed))
        {
            status = parsed.ToString();
        }
        else
        {
            return $"The status must be open, all or one of {WorkOrderToolSupport.Names<WorkOrderStatus>()}.";
        }

        var priorityText = WorkOrderToolSupport.Read(arguments, "priority");
        string priority = null;

        if (priorityText is not null)
        {
            if (!WorkOrderToolSupport.TryParseEnum<WorkOrderPriority>(priorityText, out var parsedPriority))
            {
                return $"The priority must be one of {WorkOrderToolSupport.Names<WorkOrderPriority>()}.";
            }

            priority = parsedPriority.ToString();
        }

        var (page, pageSize) = WorkOrderToolSupport.ReadPaging(arguments);
        var filter = new WorkOrderListFilter
        {
            Status = status,
            TypeId = WorkOrderToolSupport.Read(arguments, "typeId"),
            Priority = priority,
            Search = WorkOrderToolSupport.Read(arguments, "search"),
            AssetId = WorkOrderToolSupport.Read(arguments, "assetId"),
        };
        var (total, workOrders) = await WorkOrderListQuery.PageAsync(services.GetRequiredService<ISession>(), filter, (page - 1) * pageSize, pageSize, cancellationToken);
        var (types, subStatuses) = await WorkOrderToolResults.NamesAsync(services, cancellationToken);
        var items = new List<object>();

        foreach (var workOrder in workOrders)
        {
            items.Add(await WorkOrderToolResults.SummaryAsync(services, workOrder, types, subStatuses));
        }

        return WorkOrderToolSupport.Json(WorkOrderToolSupport.Paged(items, total, page, pageSize, await WorkOrderToolSupport.GetTimeZoneIdAsync(services)));
    }
}
