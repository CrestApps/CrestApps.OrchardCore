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
/// Finds colleagues who can take a worker's shift, checked against availability, other shifts, time off, skills and
/// labor rules.
/// </summary>
public sealed class FindCoverTool : WorkforceAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "workforceFindCover";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "workerName": { "type": "string", "description": "The worker whose shift needs cover." },
            "date": { "type": "string", "description": "The day of the shift, as yyyy-MM-dd." }
          },
          "required": [ "workerName", "date" ],
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Finds the colleagues in the same team who can cover a worker's shift on a day, in the teams the user manages, and why the others can't. It doesn't change the schedule.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var scope = await services.GetRequiredService<IWorkforceScopeService>().GetManagedScopeAsync(user, cancellationToken);

        if (!scope.All && scope.TeamIds.Count == 0)
        {
            return "You don't manage any team, so you can't find cover.";
        }

        var date = ReadDate(arguments, "date");
        var workerName = Read(arguments, "workerName");

        if (!date.HasValue || workerName is null)
        {
            return "Give the worker's name and the day as yyyy-MM-dd.";
        }

        var workers = services.GetRequiredService<IWorkerManager>();
        var teams = (await services.GetRequiredService<ITeamManager>().GetAllAsync(cancellationToken)).Where(team => scope.Covers(team.ItemId)).ToList();
        WorkerProfile absent = null;

        foreach (var team in teams)
        {
            absent ??= (await workers.ListByTeamAsync(team.ItemId, date.Value, cancellationToken)).FirstOrDefault(worker => WorkforceNames.Normalize(worker.DisplayName) == WorkforceNames.Normalize(workerName));
        }

        if (absent is null)
        {
            return $"No one called '{workerName}' works in the teams you manage on {date.Value:yyyy-MM-dd}.";
        }

        var shiftService = services.GetRequiredService<IShiftService>();
        var shifts = await shiftService.ListWorkerShiftsAsync(absent.ItemId, date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(-1), date.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddDays(2), cancellationToken);
        var shift = shifts.FirstOrDefault(item => DateOnly.FromDateTime(item.Start) == date.Value);

        if (shift is null || !ShiftEvents.TryRead(shift, out var data))
        {
            return $"{absent.DisplayName} has no published shift on {date.Value:yyyy-MM-dd}.";
        }

        var canCover = new List<object>();
        var cannotCover = new List<object>();

        foreach (var candidate in await workers.ListByTeamAsync(data.TeamId, date.Value, cancellationToken))
        {
            if (candidate.ItemId == absent.ItemId || candidate.Status != WorkerStatus.Active)
            {
                continue;
            }

            var problems = await shiftService.CheckAsync(new ShiftAssignmentContext
            {
                Worker = candidate,
                TeamId = data.TeamId,
                Date = date.Value,
                StartUtc = shift.StartUtc,
                EndUtc = shift.EndUtc,
                PaidMinutes = (int)(shift.EndUtc - shift.StartUtc).TotalMinutes,
            }, cancellationToken);

            if (problems.Count == 0)
            {
                canCover.Add(new { name = candidate.DisplayName });
            }
            else
            {
                cannotCover.Add(new { name = candidate.DisplayName, why = problems[0] });
            }
        }

        return JsonSerializer.Serialize(new
        {
            shift = new { worker = absent.DisplayName, start = Local(shift.Start), end = Local(shift.End) },
            canCover,
            cannotCover,
        }, JsonOptions);
    }
}
