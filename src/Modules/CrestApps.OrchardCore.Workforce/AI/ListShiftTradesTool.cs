using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Lists the shift trades of the teams the user manages, as the Shift Trades page does.
/// </summary>
internal sealed class ListShiftTradesTool : WorkforceAdminToolBase
{
    public const string ToolName = "listShiftTrades";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "teamName": { "type": "string", "description": "Optional. A team's name or ID: only its trades and its sub-teams'. Leave out for every team the user manages." },
        "status": { "type": "string", "enum": ["Offered", "Accepted", "Approved", "Declined", "Rejected", "Canceled"], "description": "Optional. Offered: waits for the other worker; Accepted: waits for a manager's approval; Approved; Declined by a manager; Rejected by the other worker; Canceled. Leave out for every status." },
        "workerId": { "type": "string", "description": "Optional. Only the trades of this worker (offered by or to them). Give this or workerName." },
        "workerName": { "type": "string", "description": "Optional. Only the trades of the worker with this name, among the workers the user manages." },
        "page": { "type": "integer", "minimum": 1, "description": "Optional. The page, from 1." },
        "pageSize": { "type": "integer", "minimum": 1, "maximum": 50, "description": "Optional. Trades per page, 20 by default, at most 50." }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Lists shift trades and giveaways of the teams the user manages, filtered by team, worker and status, with the shifts involved and, for a trade waiting for approval, the rule problems of the worker taking the shift.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ApproveShiftTrades;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (teamIds, error) = await WorkforceAdminTools.TeamFilterAsync(services, user, arguments, cancellationToken);

        if (error is not null)
        {
            return error;
        }

        ShiftTradeStatus? status = null;

        if (WorkforceAdminTools.Has(arguments, "status"))
        {
            if (!WorkforceAdminTools.TryReadEnum<ShiftTradeStatus>(arguments, "status", out var parsed))
            {
                return "The status must be Offered, Accepted, Approved, Declined, Rejected or Canceled.";
            }

            status = parsed;
        }

        string workerId = null;

        if (WorkforceAdminTools.Has(arguments, "workerId") || WorkforceAdminTools.Has(arguments, "workerName"))
        {
            var (worker, workerError) = await WorkforceAdminTools.FindWorkerAsync(services, user, arguments, cancellationToken);

            if (workerError is not null)
            {
                return workerError;
            }

            workerId = worker.ItemId;
        }

        var (page, pageSize) = WorkforceAdminTools.ReadPaging(arguments);
        var trades = services.GetRequiredService<IShiftTradeService>();
        var (count, found) = await trades.PageAsync(new ShiftTradeQuery { TeamIds = teamIds, Status = status, WorkerId = workerId }, (page - 1) * pageSize, pageSize, cancellationToken);
        var items = new List<object>();

        foreach (var trade in found)
        {
            IReadOnlyList<string> problems = null;

            if (trade.Status == ShiftTradeStatus.Accepted && !string.IsNullOrEmpty(trade.AcceptedByWorkerId))
            {
                problems = await trades.CheckAsync(trade.ItemId, trade.AcceptedByWorkerId, cancellationToken);
            }

            items.Add(await OpenShiftToolSupport.DescribeTradeAsync(services, trade, problems, cancellationToken));
        }

        return WorkforceAdminTools.Json(WorkforceAdminTools.Paged(items, count, page, pageSize));
    }
}
