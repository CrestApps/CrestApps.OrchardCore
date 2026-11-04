using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core.External;
using CrestApps.OrchardCore.Calendars.Core.Services;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CrestApps.OrchardCore.Calendars.HealthChecks;

/// <summary>
/// Reports subscribed calendars that keep failing to synchronize or whose external account refused access. A failing external
/// calendar never stops the calendar itself from working, so this reports <see cref="HealthStatus.Degraded"/>, never unhealthy.
/// </summary>
internal sealed class CalendarSyncHealthCheck : IHealthCheck
{
    /// <summary>
    /// The name the check is registered under.
    /// </summary>
    public const string Name = "calendars-external-sync";

    private const int FailuresBeforeDegraded = 3;

    private readonly ICalendarStore _calendarStore;

    public CalendarSyncHealthCheck(ICalendarStore calendarStore)
    {
        _calendarStore = calendarStore;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var calendars = await _calendarStore.ListAsync(new CalendarListQuery { Enabled = true, Limit = 1000 }, cancellationToken);
        var failing = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var calendar in calendars)
        {
            if (!calendar.TryGet<CalendarSubscription>(out var subscription) || !subscription.Enabled)
            {
                continue;
            }

            if (subscription.NeedsReconnect || subscription.Failures >= FailuresBeforeDegraded)
            {
                failing[calendar.ItemId] = subscription.NeedsReconnect ? "Needs reconnecting: " + subscription.LastError : subscription.LastError;
            }
        }

        return failing.Count == 0
            ? HealthCheckResult.Healthy("Every subscribed calendar is synchronizing.")
            : HealthCheckResult.Degraded($"{failing.Count} subscribed calendar(s) are not synchronizing.", data: failing);
    }
}
