using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Lists the open shifts of the user's own teams from today, with the user's claim on each or why they can't take it, as
/// the My Work Shifts page does.
/// </summary>
internal sealed class ListMyOpenShiftsTool : WorkforceSelfServiceToolBase
{
    public const string ToolName = "listMyOpenShifts";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "page": { "type": "integer", "minimum": 1, "description": "Optional. The page, from 1." },
        "pageSize": { "type": "integer", "minimum": 1, "maximum": 50, "description": "Optional. Shifts per page, 20 by default, at most 50." }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Lists the open shifts of the user's own teams from today, soonest first: each shift's day and times, whether the user has claimed it and, when not, why they couldn't take it. Use it before claimOpenShift.";

    public override JsonElement JsonSchema => _schema;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, StaffMember worker, CancellationToken cancellationToken)
    {
        var today = await WorkforceAdminTools.TodayAsync(services);
        var teamIds = worker.MembershipsOn(today).Select(membership => membership.TeamId).Distinct(StringComparer.Ordinal).ToArray();
        var (page, pageSize) = WorkforceAdminTools.ReadPaging(arguments);

        if (teamIds.Length == 0)
        {
            return WorkforceAdminTools.Json(WorkforceAdminTools.Paged(Array.Empty<object>(), 0, page, pageSize));
        }

        var openShifts = services.GetRequiredService<IOpenShiftService>();
        var (count, shifts) = await openShifts.PageAsync(new OpenShiftQuery { TeamIds = teamIds, From = today }, (page - 1) * pageSize, pageSize, cancellationToken);
        var items = new List<object>();

        foreach (var shift in shifts)
        {
            var claim = shift.Claims.FirstOrDefault(item => item.WorkerId == worker.ItemId && item.Status == OpenShiftClaimStatus.Pending);
            var problems = claim is null
                ? await openShifts.CheckAsync(shift.ItemId, worker.ItemId, cancellationToken)
                : [];
            var described = await OpenShiftToolSupport.DescribeShiftAsync(services, shift, withClaims: false, cancellationToken);

            items.Add(new
            {
                shift = described,
                claimedByMe = claim is not null,
                canClaim = claim is null && problems.Count == 0,
                problems,
            });
        }

        return WorkforceAdminTools.Json(WorkforceAdminTools.Paged(items, count, page, pageSize));
    }
}
