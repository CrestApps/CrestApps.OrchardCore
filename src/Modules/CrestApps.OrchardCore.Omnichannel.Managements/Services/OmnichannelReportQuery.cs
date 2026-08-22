using System.Data.SqlTypes;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Reports;
using YesSql;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Provides the shared CRM activity index queries used by the Omnichannel reports.
/// </summary>
internal static class OmnichannelReportQuery
{
    /// <summary>
    /// The lowest UTC date value that can be safely round-tripped through every supported database
    /// provider. SQL Server's <c>datetime</c> column rejects anything before 1753-01-01, so
    /// unbounded lower bounds (for example <see cref="DateTime.MinValue"/>) must be clamped to this
    /// value before they reach the query pipeline to avoid a <see cref="SqlTypeException"/>.
    /// </summary>
    public static readonly DateTime MinQueryableUtc = DateTime.SpecifyKind(SqlDateTime.MinValue.Value, DateTimeKind.Utc);

    /// <summary>
    /// The highest UTC date value that can be safely round-tripped through every supported database
    /// provider.
    /// </summary>
    public static readonly DateTime MaxQueryableUtc = DateTime.SpecifyKind(SqlDateTime.MaxValue.Value, DateTimeKind.Utc);

    /// <summary>
    /// Clamps a UTC date-range bound into the range every supported database can store, so an unset or
    /// out-of-range value never triggers a provider-level overflow when it reaches the query.
    /// </summary>
    /// <param name="value">The UTC bound to clamp.</param>
    /// <returns>The bound clamped to <see cref="MinQueryableUtc"/>..<see cref="MaxQueryableUtc"/>.</returns>
    public static DateTime ClampToQueryableRange(DateTime value)
    {
        if (value < MinQueryableUtc)
        {
            return MinQueryableUtc;
        }

        if (value > MaxQueryableUtc)
        {
            return MaxQueryableUtc;
        }

        return value;
    }

    /// <summary>
    /// Lists the activities created within the inclusive UTC period.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="fromUtc">The inclusive lower UTC bound.</param>
    /// <param name="toUtc">The inclusive upper UTC bound.</param>
    /// <param name="criteria">The optional report dimension filters.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The activities created in the period.</returns>
    public static async Task<IReadOnlyList<OmnichannelActivityIndex>> GetCreatedAsync(
        ISession session,
        DateTime fromUtc,
        DateTime toUtc,
        OmnichannelReportCriteria criteria,
        CancellationToken cancellationToken)
    {
        fromUtc = ClampToQueryableRange(fromUtc);
        toUtc = ClampToQueryableRange(toUtc);

        var activities = await session.QueryIndex<OmnichannelActivityIndex>(
            index => index.CreatedUtc >= fromUtc && index.CreatedUtc <= toUtc,
            collection: OmnichannelConstants.CollectionName)
            .ListAsync(cancellationToken);

        return Filter(activities, criteria);
    }

    /// <summary>
    /// Lists the activities completed within the inclusive UTC period.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="fromUtc">The inclusive lower UTC bound.</param>
    /// <param name="toUtc">The inclusive upper UTC bound.</param>
    /// <param name="criteria">The optional report dimension filters.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The activities completed in the period.</returns>
    public static async Task<IReadOnlyList<OmnichannelActivityIndex>> GetCompletedAsync(
        ISession session,
        DateTime fromUtc,
        DateTime toUtc,
        OmnichannelReportCriteria criteria,
        CancellationToken cancellationToken)
    {
        fromUtc = ClampToQueryableRange(fromUtc);
        toUtc = ClampToQueryableRange(toUtc);

        var activities = await session.QueryIndex<OmnichannelActivityIndex>(
            index => index.Status == ActivityStatus.Completed && index.CompletedUtc >= fromUtc && index.CompletedUtc <= toUtc,
            collection: OmnichannelConstants.CollectionName)
            .ListAsync(cancellationToken);

        return Filter(activities, criteria);
    }

    internal static IReadOnlyList<OmnichannelActivityIndex> Filter(
        IEnumerable<OmnichannelActivityIndex> activities,
        OmnichannelReportCriteria criteria)
    {
        if (criteria is null)
        {
            return activities.ToArray();
        }

        return activities
            .Where(activity => string.IsNullOrEmpty(criteria.CampaignId) || activity.CampaignId == criteria.CampaignId)
            .Where(activity => criteria.CampaignIds is null || criteria.CampaignIds.Contains(activity.CampaignId ?? string.Empty))
            .Where(activity => string.IsNullOrEmpty(criteria.Channel) || string.Equals(activity.Channel, criteria.Channel, StringComparison.OrdinalIgnoreCase))
            .Where(activity => string.IsNullOrEmpty(criteria.Source) || activity.Source == criteria.Source)
            .Where(activity => !criteria.Status.HasValue || activity.Status == criteria.Status.Value)
            .ToArray();
    }
}
