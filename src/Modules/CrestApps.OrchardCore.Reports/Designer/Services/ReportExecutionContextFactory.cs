using System.Security.Claims;
using CrestApps.OrchardCore.Reports.DataSources;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Creates the context of a designed query run: the principal whose access reads the data, the current time, the
/// tenant time zone conversions, and the configured size limits.
/// </summary>
public sealed class ReportExecutionContextFactory
{
    private readonly IClock _clock;
    private readonly ILocalClock _localClock;
    private readonly ReportQueryLimits _limits;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportExecutionContextFactory"/> class.
    /// </summary>
    /// <param name="clock">The clock.</param>
    /// <param name="localClock">The tenant local clock.</param>
    /// <param name="limits">The configured size limits.</param>
    public ReportExecutionContextFactory(
        IClock clock,
        ILocalClock localClock,
        IOptions<ReportQueryLimits> limits)
    {
        _clock = clock;
        _localClock = localClock;
        _limits = limits.Value;
    }

    /// <summary>
    /// Creates a run context.
    /// </summary>
    /// <param name="dataUser">The principal whose access reads the data.</param>
    /// <param name="dataSourceContext">An existing data source context to share, such as the one of an outer run.</param>
    /// <returns>The run context.</returns>
    public async Task<ReportQueryExecutionContext> CreateAsync(ClaimsPrincipal dataUser, ReportDataSourceContext dataSourceContext = null)
    {
        var timeZone = await _localClock.GetLocalTimeZoneAsync();
        TimeZoneInfo zone = null;

        if (!string.IsNullOrEmpty(timeZone?.TimeZoneId))
        {
            TimeZoneInfo.TryFindSystemTimeZoneById(timeZone.TimeZoneId, out zone);
        }

        zone ??= TimeZoneInfo.Utc;

        return new ReportQueryExecutionContext
        {
            DataSourceContext = dataSourceContext ?? new ReportDataSourceContext
            {
                User = dataUser,
            },
            UtcNow = _clock.UtcNow,
            ToLocal = value => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(value, DateTimeKind.Utc), zone), DateTimeKind.Unspecified),
            ToUtc = value => ToUtc(value, zone),
            Limits = new ReportQueryLimits
            {
                MaxRowsPerDataSet = _limits.MaxRowsPerDataSet,
                MaxJoinedRows = _limits.MaxJoinedRows,
                MaxResultRows = _limits.MaxResultRows,
                MaxFilterOptions = _limits.MaxFilterOptions,
            },
        };
    }

    private static DateTime ToUtc(DateTime value, TimeZoneInfo zone)
    {
        var local = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(local))
        {
            local = local.AddHours(1);
        }

        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeToUtc(local, zone), DateTimeKind.Utc);
    }
}
