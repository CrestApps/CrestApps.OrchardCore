using System.Globalization;
using CrestApps.OrchardCore.Reports;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Subscriptions.Reports;

/// <summary>
/// The expiring subscriptions report: a detail table of subscriptions due to expire within the
/// look-ahead horizon, ordered by expiration date.
/// </summary>
public sealed class ExpiringSubscriptionsReport : SubscriptionReportBase
{
    private const int HorizonDays = 30;

    private readonly ISession _session;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpiringSubscriptionsReport"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ExpiringSubscriptionsReport(
        ISession session,
        IClock clock,
        ISiteService siteService,
        IStringLocalizer<ExpiringSubscriptionsReport> stringLocalizer)
        : base(siteService, stringLocalizer)
    {
        _session = session;
        _clock = clock;
    }

    /// <inheritdoc/>
    public override string Name => "expiring-subscriptions";

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["Expiring subscriptions"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["Subscriptions due to expire within the next {0} days, ordered by expiration date.", HorizonDays];

    /// <inheritdoc/>
    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var nowUtc = _clock.UtcNow;
        var subscriptions = await _session.QueryIndex<SubscriptionIndex>().ListAsync(cancellationToken);
        var expiring = SubscriptionReportAggregator.GetExpiringSubscriptions(subscriptions, nowUtc, HorizonDays);

        var document = new ReportDocument
        {
            Title = DisplayName.Value,
        };

        var columns = new[]
        {
            new ReportColumn(S["Subscriber"].Value),
            new ReportColumn(S["Content type"].Value),
            new ReportColumn(S["Started"].Value),
            new ReportColumn(S["Expires"].Value),
            new ReportColumn(S["Days remaining"].Value, ReportColumnAlign.End),
        };

        var rows = expiring.Select(item => new ReportRow(
        [
            item.OwnerId ?? string.Empty,
            item.ContentType ?? string.Empty,
            item.StartedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            item.ExpiresAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ReportFormat.Number(item.DaysRemaining),
        ]));

        document.Add(ReportSection.ForTable(S["Expiring subscriptions"].Value, columns, rows));

        return document;
    }
}
