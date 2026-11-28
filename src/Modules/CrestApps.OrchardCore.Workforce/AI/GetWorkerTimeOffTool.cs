using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Gets a worker's time off as the worker's time-off page shows it: balances, the ledger and the policies assigned.
/// </summary>
internal sealed class GetWorkerTimeOffTool : WorkforceAdminToolBase
{
    public const string ToolName = "getWorkerTimeOff";

    private const int LedgerEntries = 20;

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "workerId": { "type": "string", "description": "The worker's ID. Give this or workerName." },
        "workerName": { "type": "string", "description": "The worker's name. Give this or workerId." },
        "date": { "type": "string", "description": "Optional. The day to work the balances out on (yyyy-MM-dd); today by default." }
      },
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Gets a worker's time-off picture: the policies assigned, the balance of each (earned, booked, pending, available) and the recent ledger entries.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ApproveTimeOff;

    protected override string NotAllowedMessage
        => $"The user isn't allowed to do this: it needs the '{WorkforcePermissions.ApproveTimeOff.Name}' or '{WorkforcePermissions.AdjustTimeOffBalances.Name}' permission.";

    // The worker's time-off page opens with either permission.
    protected override async Task<bool> IsAllowedAsync(IAuthorizationService authorizationService, ClaimsPrincipal user)
        => await authorizationService.AuthorizeAsync(user, WorkforcePermissions.ApproveTimeOff)
        || await authorizationService.AuthorizeAsync(user, WorkforcePermissions.AdjustTimeOffBalances);

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (worker, error) = await WorkforceAdminTools.FindWorkerAsync(services, user, arguments, cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var date = WorkforceAdminTools.ReadDate(arguments, "date") ?? await WorkforceAdminTools.TodayAsync(services);
        var balanceService = services.GetRequiredService<ITimeOffBalanceService>();
        var policyManager = services.GetRequiredService<ITimeOffPolicyManager>();
        var localClock = services.GetRequiredService<ILocalClock>();
        var allPolicies = (await policyManager.GetAllAsync(cancellationToken)).ToList();
        var types = (await services.GetRequiredService<ITimeOffTypeManager>().GetAllAsync(cancellationToken)).ToDictionary(type => type.ItemId, StringComparer.Ordinal);
        var balances = await balanceService.GetBalancesAsync(worker.ItemId, date, cancellationToken);
        var assigned = await policyManager.ListAssignedAsync(worker.ItemId, worker.TeamMemberships.Select(membership => membership.TeamId), cancellationToken);
        var (ledgerCount, entries) = await balanceService.PageLedgerAsync(worker.ItemId, null, 0, LedgerEntries, cancellationToken);
        var ledger = new List<object>();

        foreach (var entry in entries)
        {
            ledger.Add(new
            {
                id = entry.ItemId,
                policy = allPolicies.FirstOrDefault(policy => policy.ItemId == entry.PolicyId)?.Name,
                kind = entry.Kind.ToString(),
                amount = entry.Amount,
                effectiveDate = WorkforceAdminTools.Day(entry.EffectiveDate),
                expiresOn = WorkforceAdminTools.Day(entry.ExpiresOn),
                note = entry.Note,
                by = entry.Actor,
                recorded = await WorkforceAdminTools.LocalAsync(localClock, entry.CreatedUtc),
            });
        }

        return WorkforceAdminTools.Json(new
        {
            worker = new { id = worker.ItemId, name = worker.DisplayName },
            date = WorkforceAdminTools.Day(date),
            balances = balances.Select(balance => new
            {
                policyId = balance.Policy?.ItemId,
                policy = balance.Policy?.Name,
                type = balance.Type?.Name,
                unit = balance.Type?.Unit.ToString(),
                earned = balance.Current,
                projectedAccrual = balance.ProjectedAccrual,
                booked = balance.Upcoming,
                pending = balance.Pending,
                expiring = balance.Expiring,
                available = balance.Available,
            }),
            policies = assigned.Select(policy => new
            {
                id = policy.ItemId,
                name = policy.Name,
                type = types.GetValueOrDefault(policy.TypeId ?? string.Empty)?.Name,
                enabled = policy.Enabled,
            }),
            assignedToWorkerAlone = allPolicies
                .SelectMany(policy => policy.Assignments.Where(assignment => assignment.WorkerId == worker.ItemId).Select(assignment => new
                {
                    policyId = policy.ItemId,
                    policy = policy.Name,
                    effectiveFrom = WorkforceAdminTools.Day(assignment.EffectiveFrom),
                    effectiveTo = WorkforceAdminTools.Day(assignment.EffectiveTo),
                })),
            ledger = new
            {
                items = ledger,
                total = ledgerCount,
                truncated = ledger.Count < ledgerCount,
            },
            adminPath = WorkforceAdminTools.GetAdminPath(services, "TimeOff", "Worker", new { workerId = worker.ItemId }),
        });
    }
}
