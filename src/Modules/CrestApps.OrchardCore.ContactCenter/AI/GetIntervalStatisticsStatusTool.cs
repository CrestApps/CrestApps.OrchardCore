using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using CrestApps.OrchardCore.ContactCenter.IntervalStatistics;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.AI;

/// <summary>
/// Returns how far the 15-minute statistics are computed, as the Interval statistics page shows it.
/// </summary>
internal sealed class GetIntervalStatisticsStatusTool : ContactCenterSetupToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "getIntervalStatisticsStatus";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {},
      "required": [],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Returns the state of the 15-minute statistics: days computed and final over the last 30 days, today's totals so far, and whether a rebuild is running or stalled.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => ContactCenterPermissions.ViewReports;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var status = await IntervalStatisticsStatus.ReadAsync(
            services.GetRequiredService<IntervalStatisticsRoller>(),
            services.GetRequiredService<IIntervalStatisticsReader>(),
            services.GetRequiredService<IClock>().UtcNow,
            cancellationToken);

        return Json(await DescribeAsync(services, status));
    }

    /// <summary>
    /// Describes the status, with every time in the user's time zone. The statistics' days are calendar days and are
    /// shown as dates.
    /// </summary>
    internal static async Task<object> DescribeAsync(IServiceProvider services, IntervalStatisticsStatus status)
    {
        var localClock = services.GetService<ILocalClock>();
        var options = services.GetService<IOptions<IntervalStatisticsOptions>>()?.Value ?? new IntervalStatisticsOptions();
        var state = status.State;
        var total = status.TodayTotal;

        return new
        {
            daysComputed = status.DaysComputed,
            daysFinal = status.DaysFinal,
            ofDays = 30,
            today = new
            {
                computed = status.Today is not null,
                final = status.Today?.Final ?? false,
                computedAt = await ToLocalAsync(localClock, status.Today?.ComputedUtc),
                interactions = status.Today?.InteractionCount ?? 0,
                queues = status.QueuesToday,
                offered = total.Offered,
                answered = total.Answered,
                handled = total.Handled,
                abandoned = total.Abandoned,
                outbound = total.Outbound,
                serviceLevel = total.ServiceLevel is { } level ? Math.Round(level * 100, 1) : (double?)null,
                averageSpeedOfAnswerSeconds = Math.Round(total.AverageSpeedOfAnswerSeconds, 1),
                averageHandleSeconds = Math.Round(total.AverageHandleSeconds, 1),
            },
            rebuild = state.IsRebuilding
                ? new
                {
                    from = Day(state.RebuildFromUtc),
                    to = Day(state.RebuildToUtc),
                    next = Day(state.RebuildNextUtc),
                    requestedBy = state.RebuildRequestedBy,
                    requestedAt = await ToLocalAsync(localClock, state.RebuildRequestedUtc),
                    lastProgress = await ToLocalAsync(localClock, state.LastProgressUtc),
                    stalled = status.Stalled,
                }
                : null,
            lastRun = await ToLocalAsync(localClock, state.LastRunUtc),
            lastError = state.LastError,
            retentionDays = options.RetentionDays,
            daysPerRun = options.MaxDaysPerRun,
            note = status.Stalled
                ? "The rebuild has made no progress for over 30 minutes. Check that background tasks are running and look at the site log."
                : null,
        };
    }

    /// <summary>
    /// Writes a day of the statistics as a date.
    /// </summary>
    internal static string Day(DateTime? value)
        => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
