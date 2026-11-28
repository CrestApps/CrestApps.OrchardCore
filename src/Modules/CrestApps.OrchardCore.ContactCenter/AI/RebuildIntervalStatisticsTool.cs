using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.IntervalStatistics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Security.Permissions;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Asks for a range of days of the interval statistics to be computed again, as the Rebuild form of the Interval
/// statistics page does (<see cref="IntervalStatisticsRoller.RequestRebuildAsync"/>). It only queues the rebuild: the
/// background task computes a few days every five minutes.
/// </summary>
internal sealed class RebuildIntervalStatisticsTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "rebuildIntervalStatistics";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "from": {
          "type": "string",
          "description": "The first day to compute again, as a date (yyyy-MM-dd)."
        },
        "to": {
          "type": "string",
          "description": "The last day to compute again, as a date (yyyy-MM-dd). Days after today are not computed."
        }
      },
      "required": ["from", "to"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Queues the interval statistics of a date range to be computed again in the background (a few days every five minutes). Use it after correcting data or when numbers look wrong. It replaces any rebuild already queued.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ContactCenterPermissions.ManageContactCenter;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var reader = new ContactCenterToolArguments(arguments);

        if (!TryReadDay(reader.GetString("from"), out var from) || !TryReadDay(reader.GetString("to"), out var to))
        {
            return "Nothing was queued: give from and to as dates, for example 2026-11-02.";
        }

        if (to < from)
        {
            return "Nothing was queued: the last day must not be before the first day.";
        }

        IntervalStatisticsState state;

        try
        {
            state = await services.GetRequiredService<IntervalStatisticsRoller>().RequestRebuildAsync(from, to, user.Identity?.Name, cancellationToken);
        }
        catch (ConcurrencyException)
        {
            return "Nothing was queued: the statistics were being updated at the same moment. Try again.";
        }
        catch (ArgumentException)
        {
            // The range is clamped to the retention window and today first; a range wholly outside them is left empty.
            return "Nothing was queued: the days given are outside the days the statistics keep (they are not after today and not older than the retention period).";
        }

        var days = (int)(state.RebuildToUtc.Value - state.RebuildFromUtc.Value).TotalDays + 1;
        var daysPerRun = Math.Max(1, services.GetService<IOptions<IntervalStatisticsOptions>>()?.Value.MaxDaysPerRun ?? 5);

        return Json(new
        {
            queued = new
            {
                from = GetIntervalStatisticsStatusTool.Day(state.RebuildFromUtc),
                to = GetIntervalStatisticsStatusTool.Day(state.RebuildToUtc),
                days,
                daysPerRun,
            },
            note = $"The rebuild was queued, not run: the statistics of {days} day(s) are computed again in the background, {daysPerRun} days every five minutes. The range was kept within the retention period and today. Check progress with getIntervalStatisticsStatus.",
        });
    }

    private static bool TryReadDay(string text, out DateTime day)
    {
        day = default;

        if (text is null ||
            !DateTime.TryParseExact(text, ["yyyy-MM-dd", "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        day = DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);

        return true;
    }
}
