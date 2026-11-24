using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TimeTracker.Core.Services;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.TimeTracker.AI;

/// <summary>
/// The signed-in person's own clock, as My Time shows it: whether they are clocked in and in which stage, today's entries,
/// the week's hours by category and the stages they can switch to.
/// </summary>
internal sealed class GetMyTimeStatusTool : TimeTrackerToolBase
{
    public const string ToolName = "getMyTimeStatus";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {},
      "required": [],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Says whether the user is clocked in, in which stage and since when, with today's entries, the week's hours by category and the stages they can switch to. Only the user's own time.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => TimeTrackerPermissions.TrackOwnTime;

    protected override string SignInMessage => SelfSignInRequired;

    // A worker on the Workforce time clock reads their state with that clock's permissions, as in My Work.
    protected override async Task<bool> IsAllowedAsync(IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
        => await base.IsAllowedAsync(services, user, cancellationToken) || await IsKeptByPunchClockAsync(services, user, cancellationToken);

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return SelfSignInRequired;
        }

        var tracker = services.GetRequiredService<ITimeTrackerService>();
        var zone = await services.GetRequiredService<TimeTrackerTimeZone>().GetAsync();
        var now = services.GetRequiredService<IClock>().UtcNow;
        var today = TimeTrackerTimeZone.ToLocal(now, zone).Date;
        var dayStart = TimeTrackerTimeZone.ToUtc(today, zone);
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var stages = await TimeTrackerTools.StagesAsync(services, cancellationToken);
        var stagesById = stages.ToDictionary(stage => stage.ItemId, StringComparer.Ordinal);
        var categories = (await services.GetRequiredService<TimeCategoryStore>().GetAllAsync(cancellationToken))
            .Where(category => category.Enabled)
            .ToDictionary(category => category.ItemId, StringComparer.Ordinal);
        var status = await tracker.GetStatusAsync(userId, cancellationToken);
        var entries = await tracker.ListAsync(new TimeEntryQuery { UserIds = [userId], FromUtc = dayStart, ToUtc = dayStart.AddDays(1) }, cancellationToken);
        var week = await tracker.GetTotalsAsync(new TimeEntryQuery { UserIds = [userId], FromUtc = TimeTrackerTimeZone.ToUtc(monday, zone), ToUtc = TimeTrackerTimeZone.ToUtc(monday.AddDays(7), zone) }, zone, byDay: false, cancellationToken);
        var external = await TimeTrackerTools.ExternalClockAsync(services, userId, cancellationToken);
        var todayEntries = new List<object>();

        foreach (var entry in entries)
        {
            todayEntries.Add(new
            {
                id = entry.ItemId,
                stage = stagesById.GetValueOrDefault(entry.StageId ?? string.Empty)?.Name,
                start = TimeTrackerTools.Local(entry.StartUtc, zone),
                end = TimeTrackerTools.Local(entry.EndUtc, zone),
                open = entry.IsOpen,
                note = entry.Note,
            });
        }

        var weekRow = week.Count > 0
            ? week[0]
            : null;
        object keptBy = null;

        if (external is not null)
        {
            var punches = services.GetServices<IExternalTimeClockPunches>().FirstOrDefault(clock => clock.ClockId == external.Id);
            var state = punches is null ? null : await punches.GetStateAsync(user, cancellationToken);

            keptBy = new
            {
                name = external.Name,
                url = external.Url,
                state = state?.State,
                since = state?.SinceUtc is { } since ? TimeTrackerTools.Local(since, zone) : null,
                nextPunches = state?.NextPunches,
                canPunchHere = state is not null && state.Refusal is null,
                refusal = state?.Refusal,
            };
        }

        return TimeTrackerTools.Json(new
        {
            clockedIn = status.IsClockedIn,
            stage = status.Open is null ? null : stagesById.GetValueOrDefault(status.Open.StageId ?? string.Empty)?.Name,
            since = status.Open is null ? null : TimeTrackerTools.Local(status.Open.StartUtc, zone),
            today = todayEntries,
            week = new
            {
                from = monday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                totalHours = TimeTrackerTools.Hours(weekRow?.TotalMinutes ?? 0),
                hoursByCategory = (weekRow?.MinutesByCategory ?? new Dictionary<string, decimal>())
                    .Where(pair => categories.ContainsKey(pair.Key))
                    .OrderBy(pair => categories[pair.Key].Position)
                    .Select(pair => new { category = categories[pair.Key].Name, hours = TimeTrackerTools.Hours(pair.Value) })
                    .ToList(),
            },
            stagesYouCanSwitchTo = stages
                .Where(stage => external is null && stage.Enabled && stage.SelfService)
                .Select(stage => new { id = stage.ItemId, name = stage.Name })
                .ToList(),
            keptBy,
            timeZone = zone,
        });
    }
}
