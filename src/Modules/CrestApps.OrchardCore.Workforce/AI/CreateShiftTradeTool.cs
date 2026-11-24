using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Offers a worker's shift to a colleague, to anyone on the team, or in a swap, on the worker's behalf, as the Shift Trades
/// page's Create form does.
/// </summary>
internal sealed class CreateShiftTradeTool : WorkforceAdminToolBase
{
    public const string ToolName = "createShiftTrade";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "workerId": { "type": "string", "description": "The ID of the worker who gives the shift away. Give this or workerName." },
        "workerName": { "type": "string", "description": "The name of the worker who gives the shift away, among the workers the user manages." },
        "shiftId": { "type": "string", "description": "The ID of the worker's published shift to offer. Leave out to get the worker's shifts of the next 60 days with their IDs." },
        "kind": { "type": "string", "enum": ["GiveAway", "Swap"], "description": "GiveAway offers the shift; Swap exchanges it for one of the colleague's shifts. GiveAway when left out." },
        "targetWorkerId": { "type": "string", "description": "Optional for a giveaway (leave out to offer it to anyone on the team), required for a swap: the colleague's ID. Or give targetWorkerName." },
        "targetWorkerName": { "type": "string", "description": "The colleague's name, among the workers the user manages." },
        "targetShiftId": { "type": "string", "description": "For a swap, the ID of the colleague's shift. Leave out to get the colleague's shifts with their IDs." },
        "note": { "type": "string", "description": "Optional. A note to the colleague." }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Offers one worker's shift to another worker, or to anyone on the team, as a giveaway or a swap for one of theirs, on the worker's behalf. The other worker is asked to accept. Leave the shift out to list the worker's shifts first.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ApproveShiftTrades;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var kind = ShiftTradeKind.GiveAway;

        if (WorkforceAdminTools.Has(arguments, "kind") && !WorkforceAdminTools.TryReadEnum(arguments, "kind", out kind))
        {
            return "The kind must be GiveAway or Swap.";
        }

        var (worker, error) = await WorkforceAdminTools.FindWorkerAsync(services, user, arguments, cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var scope = await services.GetRequiredService<IWorkforceScopeService>().GetManagedScopeAsync(user, cancellationToken);

        // As the page: the worker must be on a team the user manages.
        if (!worker.TeamMemberships.Any(membership => scope.Covers(membership.TeamId)))
        {
            return $"{worker.DisplayName} isn't on a team the user manages.";
        }

        var shiftId = WorkforceAdminTools.Read(arguments, "shiftId");
        var shifts = await UpcomingShiftsAsync(services, worker, cancellationToken);

        if (shiftId is null)
        {
            return ShiftChoices(worker, shifts, "Choose the shift to offer and call again with its shiftId.");
        }

        if (!shifts.Any(shift => shift.ItemId == shiftId))
        {
            return ShiftChoices(worker, shifts, $"The shift '{shiftId}' isn't one of {worker.DisplayName}'s shifts of the next {OpenShiftToolSupport.TradeDays} days.");
        }

        StaffMember target = null;

        if (WorkforceAdminTools.Has(arguments, "targetWorkerId") || WorkforceAdminTools.Has(arguments, "targetWorkerName"))
        {
            (target, error) = await WorkforceAdminTools.FindWorkerAsync(services, user, OpenShiftToolSupport.TargetArguments(arguments), cancellationToken);

            if (error is not null)
            {
                return error;
            }
        }

        string targetShiftId = null;

        if (kind == ShiftTradeKind.Swap)
        {
            if (target is null)
            {
                return "A swap needs the colleague (targetWorkerId or targetWorkerName) and one of their shifts.";
            }

            var targetShifts = await UpcomingShiftsAsync(services, target, cancellationToken);

            targetShiftId = WorkforceAdminTools.Read(arguments, "targetShiftId");

            if (targetShiftId is null || !targetShifts.Any(shift => shift.ItemId == targetShiftId))
            {
                return ShiftChoices(target, targetShifts, "Choose the colleague's shift to swap for and call again with its targetShiftId.");
            }
        }

        var result = await services.GetRequiredService<IShiftTradeService>().OfferAsync(new ShiftTradeOffer
        {
            Kind = kind,
            ShiftEventId = shiftId,
            TargetWorkerId = target?.ItemId,
            TargetShiftEventId = targetShiftId,
            Note = WorkforceAdminTools.Read(arguments, "note"),
        }, user.Identity?.Name, cancellationToken);

        if (!result.Succeeded)
        {
            return $"The trade wasn't offered: {string.Join(" ", result.Errors)}";
        }

        return WorkforceAdminTools.Json(new
        {
            offered = true,
            trade = await OpenShiftToolSupport.DescribeTradeAsync(services, result.Value, null, cancellationToken),
        });
    }

    private static async Task<IReadOnlyList<CalendarEvent>> UpcomingShiftsAsync(IServiceProvider services, StaffMember worker, CancellationToken cancellationToken)
    {
        var now = services.GetRequiredService<IClock>().UtcNow;

        return await services.GetRequiredService<IShiftService>().ListWorkerShiftsAsync(worker.ItemId, now, now.AddDays(OpenShiftToolSupport.TradeDays), cancellationToken);
    }

    private static string ShiftChoices(StaffMember worker, IReadOnlyList<CalendarEvent> shifts, string message)
        => WorkforceAdminTools.Json(new
        {
            offered = false,
            message,
            workerId = worker.ItemId,
            worker = worker.DisplayName,
            shifts = shifts
                .OrderBy(shift => shift.StartUtc)
                .Select(shift => new
                {
                    shiftId = shift.ItemId,
                    start = OpenShiftToolSupport.ShiftTime(shift.StartUtc, shift.TimeZoneId),
                    end = OpenShiftToolSupport.ShiftTime(shift.EndUtc, shift.TimeZoneId),
                    timeZone = shift.TimeZoneId,
                })
                .ToList(),
        });
}
