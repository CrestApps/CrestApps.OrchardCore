using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Lists the labor rule overrides recorded in the teams the user manages, newest first, as the Rule Overrides page does.
/// </summary>
internal sealed class ListLaborRuleOverridesTool : WorkforceAdminToolBase
{
    public const string ToolName = "listLaborRuleOverrides";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "teamName": { "type": "string", "description": "Optional. A team the user manages, by name or ID; its sub-teams are included. Leave out for every team the user manages." },
        "page": { "type": "integer", "minimum": 1, "description": "The page, from 1." },
        "pageSize": { "type": "integer", "minimum": 1, "maximum": 50, "description": "Overrides per page, 20 by default, at most 50." }
      },
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Lists the labor rule overrides that were recorded, newest first: the rule and its rule set, the worker and team, the problem, who overrode it, when and the reason.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageLaborRules;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (teamIds, teamError) = await WorkforceAdminTools.TeamFilterAsync(services, user, arguments, cancellationToken);

        if (teamError is not null)
        {
            return teamError;
        }

        var (page, pageSize) = WorkforceAdminTools.ReadPaging(arguments);
        var (count, overrides) = await services.GetRequiredService<ILaborRuleOverrideService>().PageAsync(teamIds, (page - 1) * pageSize, pageSize, cancellationToken);
        var setManager = services.GetRequiredService<ILaborRuleSetManager>();
        var rules = setManager.ListRules().ToDictionary(rule => rule.Key, rule => rule.DisplayName, StringComparer.Ordinal);
        var teams = (await services.GetRequiredService<ITeamManager>().ListTreeAsync(cancellationToken)).ToDictionary(entry => entry.Team.ItemId, entry => entry.Team.Name, StringComparer.Ordinal);
        var names = await ShiftScheduleToolSupport.WorkerNamesAsync(services, overrides.Select(item => item.WorkerId), cancellationToken);
        var setNames = new Dictionary<string, string>(StringComparer.Ordinal);
        var localClock = services.GetRequiredService<ILocalClock>();
        var rows = new List<object>();

        foreach (var item in overrides)
        {
            if (!string.IsNullOrEmpty(item.RuleSetId) && !setNames.ContainsKey(item.RuleSetId))
            {
                setNames[item.RuleSetId] = (await setManager.FindByIdAsync(item.RuleSetId, cancellationToken))?.Name;
            }

            rows.Add(new
            {
                id = item.ItemId,
                rule = rules.GetValueOrDefault(item.RuleKey ?? string.Empty, item.RuleKey),
                ruleKey = item.RuleKey,
                ruleSet = string.IsNullOrEmpty(item.RuleSetId) ? null : setNames.GetValueOrDefault(item.RuleSetId),
                workerId = item.WorkerId,
                worker = item.WorkerId is null ? null : names.GetValueOrDefault(item.WorkerId),
                team = teams.GetValueOrDefault(item.TeamId ?? string.Empty),
                problem = item.Message,
                reason = item.Reason,
                source = item.Source,
                scheduleId = item.PeriodId,
                overriddenBy = item.Actor,
                overriddenAt = await WorkforceAdminTools.LocalAsync(localClock, item.CreatedUtc),
            });
        }

        return WorkforceAdminTools.Json(WorkforceAdminTools.Paged(rows, count, page, pageSize));
    }
}
