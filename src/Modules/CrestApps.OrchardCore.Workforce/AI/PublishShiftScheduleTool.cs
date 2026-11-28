using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Publishes a schedule to the team's calendar, as the planner board's Publish button does: with an override reason it
/// goes ahead despite labor rules that need an override, which also needs the Override labor rules permission.
/// </summary>
internal sealed class PublishShiftScheduleTool : WorkforceAdminToolBase
{
    public const string ToolName = "publishShiftSchedule";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "scheduleId": { "type": "string", "description": "The schedule to publish, from listShiftSchedules." },
        "overrideReason": { "type": "string", "description": "Optional. Why to publish despite labor rules that need an override; it is recorded with each overridden rule. Needs the Override labor rules permission. Rules that block can't be overridden." }
      },
      "required": ["scheduleId"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Publishes a draft schedule: shifts go on the team calendar, workers are notified, and shifts nobody has are offered as open shifts. Blocked by labor rule problems unless an override reason is given; run workforceExplainViolations or getShiftSchedule first.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.PublishSchedules;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (period, team, error) = await ShiftScheduleToolSupport.FindPeriodAsync(services, user, WorkforceAdminTools.Read(arguments, "scheduleId"), cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var overrideReason = WorkforceAdminTools.Read(arguments, "overrideReason");

        if (overrideReason is not null && !await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, WorkforcePermissions.OverrideLaborRules))
        {
            return NotAllowed(WorkforcePermissions.OverrideLaborRules) + " Publish without an override reason, or ask someone who may override labor rules.";
        }

        var result = await services.GetRequiredService<ISchedulePlanningService>().PublishAsync(period.ItemId, user.Identity?.Name, overrideReason, cancellationToken);

        if (result.NeedsOverride || result.Violations.Any(violation => violation.Enforcement == LaborRuleEnforcement.Block))
        {
            // Nothing was published.
            return WorkforceAdminTools.Json(new
            {
                published = false,
                scheduleId = period.ItemId,
                team = team.Name,
                needsOverride = result.NeedsOverride,
                errors = result.Errors,
                laborRuleProblems = ShiftScheduleToolSupport.Problems(result.Violations),
                note = result.Violations.Any(violation => violation.Enforcement == LaborRuleEnforcement.Block)
                    ? "Nothing was published: a labor rule blocks it. Change the shifts with saveDraftShift, then publish again."
                    : "Nothing was published: some labor rules need an override. Fix the shifts, or publish again with an overrideReason.",
            });
        }

        return WorkforceAdminTools.Json(new
        {
            published = true,
            completeWithoutProblems = result.Succeeded,
            scheduleId = period.ItemId,
            team = team.Name,
            added = result.Added,
            changed = result.Updated,
            removed = result.Removed,
            openShiftsOffered = result.Open,
            overriddenRules = result.Violations.Count(violation => violation.Enforcement == LaborRuleEnforcement.RequireOverride),
            warnings = result.Violations.Count(violation => violation.Enforcement == LaborRuleEnforcement.Warn),
            laborRuleProblems = ShiftScheduleToolSupport.Problems(result.Violations),
            errors = result.Errors,
            link = ShiftScheduleToolSupport.PlanPath(services, period.ItemId),
        });
    }
}
