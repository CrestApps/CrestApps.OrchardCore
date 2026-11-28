using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Approves, declines or cancels a shift trade, or accepts or turns it down for the other worker, as the trade page's
/// buttons do.
/// </summary>
internal sealed class DecideShiftTradeTool : WorkforceAdminToolBase
{
    public const string ToolName = "decideShiftTrade";

    private static readonly string[] _decisions = ["Approve", "Decline", "Cancel", "Accept", "Reject"];

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "tradeId": { "type": "string", "description": "The trade's ID, from listShiftTrades." },
        "decision": { "type": "string", "enum": ["Approve", "Decline", "Cancel", "Accept", "Reject"], "description": "Approve or Decline a trade waiting for approval; Cancel withdraws it; Accept or Reject answers an offered trade for the other worker." },
        "workerId": { "type": "string", "description": "For Accept on a trade offered to anyone on the team: the ID of the worker who takes it. Or give workerName." },
        "workerName": { "type": "string", "description": "For Accept on a trade offered to anyone: the name of the worker who takes it, among the workers the user manages." },
        "note": { "type": "string", "description": "Optional. For a decline, why; both workers see it." }
      },
      "required": ["tradeId", "decision"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Approves, declines (with a note) or cancels a shift trade of a team the user manages, or accepts or rejects it for the other worker. Approving swaps the shifts on the published schedule and notifies both workers.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ApproveShiftTrades;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var decision = _decisions.FirstOrDefault(item => string.Equals(item, WorkforceAdminTools.Read(arguments, "decision"), StringComparison.OrdinalIgnoreCase));

        if (decision is null)
        {
            return "The decision must be Approve, Decline, Cancel, Accept or Reject.";
        }

        var (trade, error) = await OpenShiftToolSupport.FindManagedTradeAsync(services, user, WorkforceAdminTools.Read(arguments, "tradeId"), cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var trades = services.GetRequiredService<IShiftTradeService>();
        var actor = user.Identity?.Name;
        var previous = trade.Status.ToString();
        PlanningResult<ShiftTrade> result;

        switch (decision)
        {
            case "Approve":
                result = await trades.ApproveAsync(trade.ItemId, actor, cancellationToken);
                break;
            case "Decline":
                result = await trades.DeclineAsync(trade.ItemId, WorkforceAdminTools.Read(arguments, "note"), actor, cancellationToken);
                break;
            case "Cancel":
                result = await trades.CancelAsync(trade.ItemId, actor, cancellationToken);
                break;
            case "Reject":
                result = await trades.RejectAsync(trade.ItemId, trade.TargetWorkerId, actor, cancellationToken);
                break;
            default:
                {
                    var workerId = trade.TargetWorkerId;

                    if (string.IsNullOrEmpty(workerId))
                    {
                        var (worker, workerError) = await WorkforceAdminTools.FindWorkerAsync(services, user, arguments, cancellationToken);

                        if (workerError is not null)
                        {
                            return $"The trade is offered to anyone on the team, so say who takes it. {workerError}";
                        }

                        workerId = worker.ItemId;
                    }

                    result = await trades.AcceptAsync(trade.ItemId, workerId, actor, cancellationToken);
                    break;
                }
        }

        if (!result.Succeeded)
        {
            return $"Nothing changed: {string.Join(" ", result.Errors)}";
        }

        return WorkforceAdminTools.Json(new
        {
            decision,
            previousStatus = previous,
            trade = await OpenShiftToolSupport.DescribeTradeAsync(services, result.Value ?? trade, null, cancellationToken),
        });
    }
}
