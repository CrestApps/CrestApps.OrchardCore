using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Dispatch.Core.Services;
using CrestApps.OrchardCore.Dispatch.Services;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using IClock = OrchardCore.Modules.IClock;

namespace CrestApps.OrchardCore.Dispatch.AI;

/// <summary>
/// Gives a day of the dispatch board: each technician with their open time, time off and visits, and the visits waiting
/// for a time. Technicians' live positions are never returned.
/// </summary>
internal sealed class GetDispatchBoardTool : DispatchAdminToolBase
{
    public const string ToolName = "getDispatchBoard";

    private const int MaxUnscheduledShown = 50;

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "day": { "type": "string", "description": "Optional. The day (yyyy-MM-dd, in the dispatch time zone); today by default." },
        "team": { "type": "string", "description": "Optional. A team's name or ID: only its technicians and those of its sub-teams." },
        "capability": { "type": "string", "description": "Optional. A staff capability name; by default the dispatch settings' technician capabilities." }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Gives a day's dispatch picture: each technician with their shift, time off and visits in order, and the visits waiting for a time; optionally for one team or capability. Use it to answer \"what does tomorrow look like\".";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => DispatchPermissions.ViewDispatch;

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var zone = await DispatchAdminTools.TimeZoneAsync(services);
        DateOnly date;

        if (DispatchAdminTools.Has(arguments, "day"))
        {
            if (DispatchAdminTools.ReadDate(arguments, "day") is not { } day)
            {
                return "Give the day as yyyy-MM-dd.";
            }

            date = day;
        }
        else
        {
            date = DateOnly.FromDateTime(CalendarTime.ToLocal(services.GetRequiredService<IClock>().UtcNow, zone));
        }

        var teams = services.GetRequiredService<ITeamManager>();
        string teamId = null;

        if (DispatchAdminTools.Read(arguments, "team") is { } teamName)
        {
            var normalized = teamName.Trim();
            var team = (await teams.ListTreeAsync(cancellationToken))
                .Select(entry => entry.Team)
                .FirstOrDefault(item => item.ItemId == normalized || string.Equals(item.Name?.Trim(), normalized, StringComparison.OrdinalIgnoreCase));

            if (team is null)
            {
                return $"No team is called '{teamName}'.";
            }

            teamId = team.ItemId;
        }

        var board = await DispatchBoardDayQuery.LoadAsync(
            services.GetRequiredService<DispatchResources>(),
            services.GetRequiredService<IDispatchService>(),
            teams,
            date,
            zone,
            teamId,
            DispatchAdminTools.Read(arguments, "capability"),
            cancellationToken);
        var names = board.Rows.ToDictionary(row => row.Worker.ItemId, row => row.Worker.DisplayName, StringComparer.Ordinal);

        var technicians = board.Rows.Select(row => new
        {
            id = row.Worker.ItemId,
            name = row.Worker.DisplayName,
            team = row.Team?.Name,
            onShift = row.Availability.HasShift,
            open = row.Availability.Open.Select(span => new { from = DispatchAdminTools.Local(span.StartUtc, zone), to = DispatchAdminTools.Local(span.EndUtc, zone) }).ToArray(),
            timeOff = row.Availability.TimeOff.Select(span => new { from = DispatchAdminTools.Local(span.StartUtc, zone), to = DispatchAdminTools.Local(span.EndUtc, zone) }).ToArray(),
            visits = board.Scheduled
                .Where(visit => visit.WorkerIds.Contains(row.Worker.ItemId))
                .OrderBy(visit => visit.StartUtc)
                .Select(visit => new
                {
                    id = visit.ItemId,
                    number = visit.Number,
                    summary = visit.Summary,
                    status = visit.Status.ToString(),
                    start = DispatchAdminTools.Local(visit.StartUtc, zone),
                    end = DispatchAdminTools.Local(visit.EndUtc, zone),
                    travelMinutes = visit.TravelMinutes,
                    lead = visit.LeadWorkerId == row.Worker.ItemId,
                    pinned = visit.Pinned,
                    atRisk = visit.AtRiskReason,
                })
                .ToArray(),
        }).ToList();

        // Visits given to people the board doesn't show (other capabilities), so nothing scheduled goes unmentioned.
        var others = board.Scheduled
            .Where(visit => !visit.WorkerIds.Any(names.ContainsKey))
            .Select(visit => DispatchAdminTools.Row(visit, names, zone))
            .ToList();

        return DispatchAdminTools.Json(new
        {
            day = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            timeZone = zone,
            technicians,
            otherScheduledVisits = others,
            unscheduled = board.Unscheduled.Take(MaxUnscheduledShown).Select(visit => DispatchAdminTools.Row(visit, names, zone)).ToArray(),
            unscheduledCount = board.Unscheduled.Count,
            unscheduledTruncated = board.Unscheduled.Count > MaxUnscheduledShown,
        });
    }
}
