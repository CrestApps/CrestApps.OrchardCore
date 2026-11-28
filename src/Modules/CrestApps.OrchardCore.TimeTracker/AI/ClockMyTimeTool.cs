using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.TimeTracker.AI;

/// <summary>
/// Clocks the signed-in person in, switches their stage or clocks them out, as the My Time buttons do. When another clock
/// keeps the person's time (the Workforce time clock), the punch goes to that clock, with its own checks, as its My Work
/// buttons do; a clock that takes no punches from here is named with its link.
/// </summary>
internal sealed class ClockMyTimeTool : TimeTrackerToolBase
{
    public const string ToolName = "clockMyTime";

    private static readonly JsonElement _schema = Schema("""
    {
      "type": "object",
      "properties": {
        "stage": { "type": "string", "description": "The stage to clock in to or switch to (its name or ID, such as Work, Break or Lunch), from getMyTimeStatus. Leave it out to clock out." },
        "clockOut": { "type": "boolean", "description": "true to clock out. Give either stage or clockOut." },
        "punch": { "type": "string", "enum": ["In", "Out", "MealStart", "MealEnd", "BreakStart", "BreakEnd"], "description": "Only when another clock (such as the Workforce time clock) keeps the user's time: the punch to make, one of the next punches getMyTimeStatus lists." }
      },
      "required": [],
      "additionalProperties": false
    }
    """);

    public override string Name => ToolName;

    public override string Description => "Clocks the user in or switches them to another stage (such as Work, Break or Lunch), or clocks them out, now. When the Workforce time clock keeps their time, it punches there instead (In, Out, MealStart, MealEnd, BreakStart, BreakEnd), with that clock's rules. Only the user's own time.";

    public override JsonElement JsonSchema => _schema;

    protected override Permission Permission => TimeTrackerPermissions.TrackOwnTime;

    protected override string SignInMessage => SelfSignInRequired;

    // A worker on the Workforce time clock punches with that clock's permissions, as in My Work, without Track one's own time.
    protected override async Task<bool> IsAllowedAsync(IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
        => await base.IsAllowedAsync(services, user, cancellationToken) || await IsKeptByPunchClockAsync(services, user, cancellationToken);

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return SelfSignInRequired;
        }

        var stageName = TimeTrackerTools.Read(arguments, "stage");
        var clockOut = TimeTrackerTools.ReadBool(arguments, "clockOut") == true;
        var punch = TimeTrackerTools.Read(arguments, "punch");
        var zone = await services.GetRequiredService<TimeTrackerTimeZone>().GetAsync();
        var external = await TimeTrackerTools.ExternalClockAsync(services, userId, cancellationToken);

        if (external is not null)
        {
            return await PunchElsewhereAsync(services, user, external, punch, clockOut, zone, cancellationToken);
        }

        if (punch is not null)
        {
            return "Time Tracker keeps the user's time, so there is no punch to make: give a stage to clock in or switch to, or clockOut true.";
        }

        if (stageName is not null && clockOut)
        {
            return "Give either a stage to clock in or switch to, or clockOut true, not both.";
        }

        var tracker = services.GetRequiredService<ITimeTrackerService>();

        if (clockOut)
        {
            var result = await tracker.ClockOutAsync(userId, user.Identity?.Name, cancellationToken);

            if (!result.Succeeded)
            {
                return "Nothing changed: " + string.Join(" ", result.Errors);
            }

            await TimeTrackerTools.SaveAsync(services, cancellationToken);

            return TimeTrackerTools.Json(new
            {
                clockedOut = true,
                entryId = result.Entry.ItemId,
                at = TimeTrackerTools.Local(result.Entry.EndUtc, zone),
                timeZone = zone,
            });
        }

        var stages = await TimeTrackerTools.StagesAsync(services, cancellationToken);
        var choices = stages.Where(item => item.Enabled && item.SelfService).Select(item => item.Name).ToList();

        if (stageName is null)
        {
            return $"Give the stage to clock in or switch to ({string.Join(", ", choices)}), or clockOut true.";
        }

        var stage = TimeTrackerTools.FindStage(stages, stageName);

        // As My Time: only a stage people may choose themselves.
        if (stage is null || !stage.SelfService)
        {
            return $"Choose one of the user's stages: {string.Join(", ", choices)}.";
        }

        var switched = await tracker.SwitchAsync(userId, await TimeTrackerTools.CallerNameAsync(services, user, cancellationToken), stage.ItemId, TimeTrackerConstants.Sources.Self, user.Identity?.Name, null, cancellationToken);

        if (!switched.Succeeded)
        {
            return "Nothing changed: " + string.Join(" ", switched.Errors);
        }

        await TimeTrackerTools.SaveAsync(services, cancellationToken);

        return TimeTrackerTools.Json(new
        {
            clockedIn = true,
            stage = stage.Name,
            entryId = switched.Entry.ItemId,
            since = TimeTrackerTools.Local(switched.Entry.StartUtc, zone),
            timeZone = zone,
        });
    }

    private static async Task<object> PunchElsewhereAsync(IServiceProvider services, ClaimsPrincipal user, ExternalTimeClockInfo external, string punch, bool clockOut, string zone, CancellationToken cancellationToken)
    {
        var clock = services.GetServices<IExternalTimeClockPunches>().FirstOrDefault(item => item.ClockId == external.Id);

        if (clock is null)
        {
            return string.IsNullOrEmpty(external.Url)
                ? $"Nothing changed: the user's time is kept by {external.Name}, which takes punches only on its own page or device."
                : $"Nothing changed: the user's time is kept by {external.Name}; they clock in and out at {external.Url}.";
        }

        // Clocking out is the Out punch; a stage means nothing to the other clock.
        punch ??= clockOut ? "Out" : null;

        if (punch is null)
        {
            var state = await clock.GetStateAsync(user, cancellationToken);

            if (state.Refusal is not null)
            {
                return "Nothing changed: " + state.Refusal;
            }

            return $"The user's time is kept by {external.Name}, so give the punch to make: {string.Join(", ", state.NextPunches)}.";
        }

        var result = await clock.PunchAsync(user, punch, cancellationToken);

        if (!result.Succeeded)
        {
            return "Nothing changed: " + string.Join(" ", result.Errors);
        }

        await TimeTrackerTools.SaveAsync(services, cancellationToken);

        // The worker's own time zone, as their clock shows it; the Time Tracker's when it is unknown.
        var timeZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(result.TimeZoneId ?? string.Empty) is null ? zone : result.TimeZoneId;

        return TimeTrackerTools.Json(new
        {
            punched = result.Punch,
            clock = external.Name,
            at = TimeTrackerTools.Local(result.Utc, timeZone),
            timeZone,
            awayFromWorkSite = result.WithinGeofence == false,
        });
    }
}
