using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Workforce.Controllers;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Workforce.AI;

/// <summary>
/// Lists the user's own published shifts.
/// </summary>
public sealed class MyScheduleTool : WorkforceAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "workforceMySchedule";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "from": { "type": "string", "description": "The first day, as yyyy-MM-dd. Leave out for today." },
            "days": { "type": "integer", "minimum": 1, "maximum": 28, "description": "How many days to list, 7 when left out." }
          },
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Lists the user's own published shifts, with their team and local start and end times.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var worker = await WorkerOfAsync(services, user);

        if (worker is null)
        {
            return "Your account isn't linked to a worker, so you have no schedule.";
        }

        var days = Math.Clamp(ReadInt(arguments, "days") ?? 7, 1, 28);
        var from = ReadDate(arguments, "from") ?? await PlanningOptions.TodayAsync(services.GetRequiredService<ILocalClock>());
        var teams = (await services.GetRequiredService<ITeamManager>().GetAllAsync(cancellationToken)).ToDictionary(team => team.ItemId, team => team.Name, StringComparer.Ordinal);
        var shifts = await services.GetRequiredService<IShiftService>().ListWorkerShiftsAsync(
            worker.ItemId,
            from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(-1),
            from.AddDays(days + 1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            cancellationToken);

        return JsonSerializer.Serialize(new
        {
            shifts = shifts
                .Where(shift => DateOnly.FromDateTime(shift.Start) >= from && DateOnly.FromDateTime(shift.Start) < from.AddDays(days))
                .OrderBy(shift => shift.StartUtc)
                .Select(shift => new
                {
                    team = ShiftEvents.TryRead(shift, out var data) ? teams.GetValueOrDefault(data.TeamId ?? string.Empty) : null,
                    start = Local(shift.Start),
                    end = Local(shift.End),
                }),
        }, JsonOptions);
    }
}
