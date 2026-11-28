using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Lists the open shifts of the teams the user manages, as the Open Shifts page does.
/// </summary>
internal sealed class ListOpenShiftsTool : WorkforceAdminToolBase
{
    public const string ToolName = "listOpenShifts";

    // Finding the eligible workers checks every team member against the shift's rules, so it is done for a short page only.
    private const int MaxEligiblePageSize = 10;

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "teamName": { "type": "string", "description": "Optional. A team's name or ID: only its open shifts and its sub-teams'. Leave out for every team the user manages." },
        "status": { "type": "string", "enum": ["Open", "Claimed", "Filled", "Canceled"], "description": "Optional. Open: nobody has claimed it; Claimed: claims wait for a decision; Filled; Canceled. Leave out for open and claimed shifts." },
        "from": { "type": "string", "description": "Optional. The first day, as yyyy-MM-dd: shifts on or after it." },
        "includeEligible": { "type": "boolean", "description": "Optional. Also list, for each open or claimed shift, the workers who could take it. Only with a page size of 10 or less." },
        "page": { "type": "integer", "minimum": 1, "description": "Optional. The page, from 1." },
        "pageSize": { "type": "integer", "minimum": 1, "maximum": 50, "description": "Optional. Shifts per page, 20 by default, at most 50." }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Lists open shifts of the teams the user manages, filtered by team, status and date, soonest first, each with its claims and, when asked, the workers who are eligible to take it.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageOpenShifts;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (teamIds, error) = await WorkforceAdminTools.TeamFilterAsync(services, user, arguments, cancellationToken);

        if (error is not null)
        {
            return error;
        }

        OpenShiftStatus? status = null;

        if (WorkforceAdminTools.Has(arguments, "status"))
        {
            if (!WorkforceAdminTools.TryReadEnum<OpenShiftStatus>(arguments, "status", out var parsed))
            {
                return "The status must be Open, Claimed, Filled or Canceled.";
            }

            status = parsed;
        }

        var from = WorkforceAdminTools.ReadDate(arguments, "from");

        if (WorkforceAdminTools.Has(arguments, "from") && from is null)
        {
            return "Give the first day as yyyy-MM-dd.";
        }

        var (page, pageSize) = WorkforceAdminTools.ReadPaging(arguments);
        var includeEligible = WorkforceAdminTools.ReadBool(arguments, "includeEligible") == true;

        if (includeEligible && pageSize > MaxEligiblePageSize)
        {
            return $"Ask for the eligible workers with a page size of {MaxEligiblePageSize} or less.";
        }

        var openShifts = services.GetRequiredService<IOpenShiftService>();
        var (count, shifts) = await openShifts.PageAsync(new OpenShiftQuery { TeamIds = teamIds, Status = status, From = from }, (page - 1) * pageSize, pageSize, cancellationToken);
        var items = new List<object>();

        foreach (var shift in shifts)
        {
            var described = await OpenShiftToolSupport.DescribeShiftAsync(services, shift, withClaims: true, cancellationToken);

            if (includeEligible && shift.Status is OpenShiftStatus.Open or OpenShiftStatus.Claimed)
            {
                var eligible = (await openShifts.ListEligibleWorkersAsync(shift.ItemId, cancellationToken))
                    .Select(worker => new { workerId = worker.ItemId, worker = worker.DisplayName })
                    .ToList();

                items.Add(new { shift = described, eligibleWorkers = eligible });
            }
            else
            {
                items.Add(described);
            }
        }

        return WorkforceAdminTools.Json(WorkforceAdminTools.Paged(items, count, page, pageSize));
    }
}
