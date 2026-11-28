using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Fills a draft schedule as the planner board's buttons do: Apply rotations, Copy from another schedule of the team, or
/// Auto-schedule against the coverage requirements, with a short time limit for the solver.
/// </summary>
internal sealed class FillShiftScheduleTool : WorkforceAdminToolBase
{
    public const string ToolName = "fillShiftSchedule";

    /// <summary>
    /// The solver's time limit when none is given, in seconds.
    /// </summary>
    public const int DefaultSolverSeconds = 20;

    /// <summary>
    /// The longest the solver may run from a chat call, in seconds.
    /// </summary>
    public const int MaxSolverSeconds = 30;

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "scheduleId": { "type": "string", "description": "The schedule to fill, from listShiftSchedules." },
        "method": { "type": "string", "enum": ["Rotations", "Copy", "AutoSchedule"], "description": "Rotations adds the shifts of the team's rotations; Copy adds the shifts of another schedule of the same team; AutoSchedule runs the scheduler against the team's coverage requirements." },
        "sourceScheduleId": { "type": "string", "description": "For Copy: the schedule of the same team to copy from." },
        "timeLimitSeconds": { "type": "integer", "minimum": 5, "maximum": 30, "description": "For AutoSchedule: how long the scheduler may run, 20 seconds by default, at most 30." }
      },
      "required": ["scheduleId", "method"],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Fills a draft schedule in one of three ways: apply the team's rotations, copy the shifts of another period, or run the auto-scheduler against the coverage requirements. Returns how many shifts were added and the days still short of coverage.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => WorkforcePermissions.ManageSchedules;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var (period, team, error) = await ShiftScheduleToolSupport.FindPeriodAsync(services, user, WorkforceAdminTools.Read(arguments, "scheduleId"), cancellationToken);

        if (error is not null)
        {
            return error;
        }

        var method = WorkforceAdminTools.Read(arguments, "method");
        var planning = services.GetRequiredService<ISchedulePlanningService>();
        var actor = user.Identity?.Name;
        object run = null;
        IList<string> warnings = [];
        int added;

        if (string.Equals(method, "Rotations", StringComparison.OrdinalIgnoreCase))
        {
            added = await planning.ApplyPatternsAsync(period.PrimaryScenarioId, actor, cancellationToken);
        }
        else if (string.Equals(method, "Copy", StringComparison.OrdinalIgnoreCase))
        {
            var sourceId = WorkforceAdminTools.Read(arguments, "sourceScheduleId");
            var source = sourceId is null ? null : await planning.FindPeriodAsync(sourceId, cancellationToken);

            if (source is null || source.TeamId != period.TeamId || source.ItemId == period.ItemId)
            {
                return "Give another schedule of the same team to copy from (sourceScheduleId), from listShiftSchedules.";
            }

            added = await planning.CopyFromPeriodAsync(period.PrimaryScenarioId, source.ItemId, actor, cancellationToken);
        }
        else if (string.Equals(method, "AutoSchedule", StringComparison.OrdinalIgnoreCase))
        {
            var seconds = Math.Clamp(WorkforceAdminTools.ReadInt(arguments, "timeLimitSeconds") ?? DefaultSolverSeconds, 5, MaxSolverSeconds);
            var result = await planning.AutoScheduleAsync(period.PrimaryScenarioId, new AutoScheduleOptions { TimeLimitSeconds = seconds }, actor, cancellationToken);

            warnings = result.Errors;

            if (result.Run is null)
            {
                return "The scheduler did not run: " + (result.Errors.Count == 0 ? "the schedule may be locked." : string.Join(" ", result.Errors));
            }

            added = result.Run.ShiftsAdded;
            run = new
            {
                solver = result.Run.Solver,
                coverageOfPeopleWanted = Math.Round(result.Run.CoverageScore, 3),
                coverageOfFewestNeeded = Math.Round(result.Run.MinimumScore, 3),
                seconds = Math.Round(result.Run.RuntimeMilliseconds / 1000d, 1),
                stoppedAtTimeLimit = result.Run.TimedOut,
                note = result.Run.TimedOut ? "The scheduler stopped at its time limit; running it again may add more." : null,
            };
        }
        else
        {
            return "The method must be Rotations, Copy or AutoSchedule.";
        }

        var coverage = await ShiftScheduleToolSupport.CoverageAsync(services, period, team, period.StartDate, period.EndDate, cancellationToken);

        return WorkforceAdminTools.Json(new
        {
            scheduleId = period.ItemId,
            method,
            shiftsAdded = added,
            run,
            warnings,
            coverage,
            link = ShiftScheduleToolSupport.PlanPath(services, period.ItemId),
        });
    }
}
