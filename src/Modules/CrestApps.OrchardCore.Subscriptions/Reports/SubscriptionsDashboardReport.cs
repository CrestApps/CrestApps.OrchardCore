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
/// The subscriptions dashboard report: active, new, and expiring subscriptions plus the distinct
/// subscriber count for the reporting period.
/// </summary>
public sealed class SubscriptionsDashboardReport : SubscriptionReportBase
{
    private const int ExpiringHorizonDays = 30;

    private readonly ISession _session;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionsDashboardReport"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public SubscriptionsDashboardReport(
        ISession session,
        IClock clock,
        ISiteService siteService,
        IStringLocalizer<SubscriptionsDashboardReport> stringLocalizer)
        : base(siteService, stringLocalizer)
    {
        _session = session;
        _clock = clock;
    }

    /// <inheritdoc/>
    public override string Name => "subscriptions-dashboard";

    /// <inheritdoc/>
    public override LocalizedString DisplayName => S["Subscriptions dashboard"];

    /// <inheritdoc/>
    public override LocalizedString Description => S["Active, new, and expiring subscriptions plus the total number of distinct subscribers."];

    /// <inheritdoc/>
    public override string Category => ReportsConstants.Categories.Executive;

    /// <inheritdoc/>
    public override async Task<ReportDocument> RunAsync(ReportContext context, CancellationToken cancellationToken = default)
    {
        var range = context.Filter.GetDateRange();
        var nowUtc = _clock.UtcNow;
        var subscriptions = await _session.QueryIndex<SubscriptionIndex>().ListAsync(cancellationToken);
        var summary = SubscriptionReportAggregator.SummarizeDashboard(
            subscriptions,
            nowUtc,
            range.FromUtc,
            range.ToUtc,
            ExpiringHorizonDays);

        var document = new ReportDocument
        {
            Title = DisplayName.Value,
        };

        document.Add(ReportSection.ForMetrics(S["Summary"].Value,
        [
            new ReportMetric(S["Active subscriptions"].Value, ReportFormat.Number(summary.ActiveSubscriptions)),
            new ReportMetric(S["New subscriptions"].Value, ReportFormat.Number(summary.NewSubscriptions)),
            new ReportMetric(S["Expiring soon"].Value, ReportFormat.Number(summary.ExpiringSubscriptions), S["Next {0} days", ExpiringHorizonDays].Value),
            new ReportMetric(S["Total subscribers"].Value, ReportFormat.Number(summary.TotalSubscribers)),
        ]));

        return document;
    }
}
