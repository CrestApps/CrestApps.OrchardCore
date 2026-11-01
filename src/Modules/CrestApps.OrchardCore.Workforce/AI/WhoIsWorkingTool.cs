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
/// Lists who is on shift at a time in the teams the user manages, and whether they are clocked in.
/// </summary>
public sealed class WhoIsWorkingTool : WorkforceAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string ToolName = "workforceWhoIsWorking";

    private static readonly JsonElement _schema = Schema("""
        {
          "type": "object",
          "properties": {
            "at": { "type": "string", "description": "The local date and time to look at, as yyyy-MM-ddTHH:mm in each team's time zone. Leave out for now." },
            "teamName": { "type": "string", "description": "Optional. One team the user manages; its sub-teams are included." }
          },
          "additionalProperties": false
        }
        """);

    /// <inheritdoc />
    public override string Name => ToolName;

    /// <inheritdoc />
    public override string Description => "Lists who is on shift at a time in the teams the user manages, with each person's team, shift times and whether they are clocked in.";

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <inheritdoc />
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var scope = await services.GetRequiredService<IWorkforceScopeService>().GetManagedScopeAsync(user, cancellationToken);
        var teamManager = services.GetRequiredService<ITeamManager>();
        var teams = (await teamManager.ListTreeAsync(cancellationToken)).Select(entry => entry.Team).ToList();
        var teamName = Read(arguments, "teamName");

        if (!scope.All && scope.TeamIds.Count == 0)
        {
            return "You don't manage any team, so you can't see who is working.";
        }

        if (teamName is not null)
        {
            var named = teams.FirstOrDefault(team => WorkforceNames.Normalize(team.Name) == WorkforceNames.Normalize(teamName));

            if (named is null)
            {
                return $"No team is called '{teamName}'.";
            }

            if (!scope.Covers(named.ItemId))
            {
                return $"You don't manage {named.Name}, so you can't see who is working there.";
            }

            var subtree = await teamManager.GetSubtreeIdsAsync(named.ItemId, cancellationToken);

            teams = teams.Where(team => subtree.Contains(team.ItemId)).ToList();
        }

        teams = teams.Where(team => scope.Covers(team.ItemId) && !string.IsNullOrEmpty(team.CalendarId)).ToList();

        var at = ReadDateTime(arguments, "at");
        var queries = services.GetRequiredService<ICalendarQueryService>();
        var siteService = services.GetRequiredService<ISiteService>();
        var workers = services.GetRequiredService<IWorkerManager>();
        var timeClock = services.GetService<ITimeClockService>();
        var clock = services.GetRequiredService<IClock>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var working = new List<object>();

        foreach (var team in teams)
        {
            var zone = await PlanningOptions.TimeZoneOfAsync(team, siteService);
            var atUtc = at.HasValue ? CalendarTime.ToUtc(at.Value, zone) : clock.UtcNow;
            var occurrences = await queries.GetOccurrencesAsync(new CalendarOccurrenceQuery
            {
                CalendarIds = [team.CalendarId],
                FromUtc = atUtc,
                ToUtc = atUtc.AddMinutes(1),
                EventTypes = [WorkforceConstants.EventTypes.Shift],
                Sources = [WorkforceConstants.Source],
            }, cancellationToken);

            foreach (var occurrence in occurrences)
            {
                if (occurrence.Event.Status == CalendarEventStatus.Cancelled || !ShiftEvents.TryRead(occurrence.Event, out var data) || string.IsNullOrEmpty(data.WorkerId) || !seen.Add(occurrence.Event.ItemId))
                {
                    continue;
                }

                var worker = await workers.FindByIdAsync(data.WorkerId, cancellationToken);
                string clockState = null;

                if (timeClock is not null && worker is not null && !at.HasValue)
                {
                    clockState = (await timeClock.GetStatusAsync(worker, cancellationToken)).State.ToString();
                }

                working.Add(new
                {
                    name = worker?.DisplayName,
                    team = team.Name,
                    start = Local(CalendarTime.ToLocal(occurrence.StartUtc, zone)),
                    end = Local(CalendarTime.ToLocal(occurrence.EndUtc, zone)),
                    clock = clockState,
                });
            }
        }

        return JsonSerializer.Serialize(new { at = at.HasValue ? Local(at.Value) : "now", working }, JsonOptions);
    }
}
