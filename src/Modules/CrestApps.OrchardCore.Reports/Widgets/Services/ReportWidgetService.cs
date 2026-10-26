using System.Globalization;
using System.Security.Claims;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using CrestApps.OrchardCore.Reports.Widgets.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Widgets.Services;

/// <summary>
/// Runs the report a dashboard widget shows, for the period the widget covers, and keeps the result for the minutes the
/// widget asks so the dashboard doesn't run a report on every visit.
/// </summary>
public sealed class ReportWidgetService
{
    private readonly IReportManager _reportManager;
    private readonly IAuthorizationService _authorizationService;
    private readonly ReportDisplayValueResolver _displayValueResolver;
    private readonly IStringLocalizerFactory _localizerFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ILocalClock _localClock;
    private readonly IClock _clock;
    private readonly ILogger<ReportWidgetService> _logger;

    public ReportWidgetService(
        IReportManager reportManager,
        IAuthorizationService authorizationService,
        ReportDisplayValueResolver displayValueResolver,
        IStringLocalizerFactory localizerFactory,
        IMemoryCache memoryCache,
        ILocalClock localClock,
        IClock clock,
        ILogger<ReportWidgetService> logger)
    {
        _reportManager = reportManager;
        _authorizationService = authorizationService;
        _displayValueResolver = displayValueResolver;
        _localizerFactory = localizerFactory;
        _memoryCache = memoryCache;
        _localClock = localClock;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Gets what the widget shows.
    /// </summary>
    /// <param name="part">The widget.</param>
    /// <param name="settings">The settings of the widget's type.</param>
    /// <param name="user">The person looking at the dashboard.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The outcome, which says why when there is nothing to show.</returns>
    public async Task<ReportWidgetResult> GetAsync(ReportWidgetPart part, ReportWidgetPartSettings settings, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(settings);

        var reportName = string.IsNullOrEmpty(settings.ReportName) ? part.ReportName : settings.ReportName;
        var descriptor = string.IsNullOrEmpty(reportName) ? null : _reportManager.GetDescriptor(reportName);

        if (descriptor is null)
        {
            return ReportWidgetResult.NotConfigured();
        }

        // Whoever sees the dashboard sees what they may see in Reports, no more: the kept document is shared.
        if (!await _authorizationService.AuthorizeAsync(user, descriptor.Permission))
        {
            return ReportWidgetResult.Denied(descriptor);
        }

        var period = part.Period ?? settings.DefaultPeriod;
        var content = part.Show ?? settings.DefaultContent;
        var maxRows = Math.Clamp(part.MaxRows ?? settings.DefaultMaxRows, 0, 100);
        var minutes = Math.Clamp(part.CacheMinutes, 0, 24 * 60);
        var culture = CultureInfo.CurrentUICulture.Name;
        var key = $"CrestApps.ReportWidget:{part.ContentItem.ContentItemId}:{part.ContentItem.ModifiedUtc?.Ticks}:{descriptor.Name}:{period}:{content}:{maxRows}:{culture}";

        if (minutes > 0 && _memoryCache.TryGetValue(key, out CachedReportWidget cached))
        {
            return ReportWidgetResult.Success(descriptor, cached.Document, cached.GeneratedUtc, cached.From, cached.To);
        }

        try
        {
            var localNow = (await _localClock.ConvertToLocalAsync(_clock.UtcNow)).DateTime;
            var (fromLocal, toLocal) = ReportWidgetPeriods.Resolve(period, localNow, part.From, part.To);
            var fromUtc = fromLocal.HasValue ? await _localClock.ConvertToUtcAsync(DateTime.SpecifyKind(fromLocal.Value, DateTimeKind.Unspecified)) : (DateTime?)null;
            var toUtc = toLocal.HasValue ? await _localClock.ConvertToUtcAsync(DateTime.SpecifyKind(toLocal.Value, DateTimeKind.Unspecified)) : (DateTime?)null;

            var filter = new ReportFilter { ReportName = descriptor.Name };
            filter.SetDateRange(new ReportDateRange { FromUtc = fromUtc, ToUtc = toUtc, Key = period.ToString() });

            var document = await _reportManager.GetReport(descriptor.Name).RunAsync(new ReportContext(filter), cancellationToken);
            document.Title = _localizerFactory.Localize(descriptor.DisplayName).Value;

            await _displayValueResolver.ResolveNonTableValuesAsync(document);
            ReportWidgetTrimmer.Trim(document, content, maxRows);

            var generated = _clock.UtcNow;

            if (minutes > 0)
            {
                _memoryCache.Set(key, new CachedReportWidget(document, generated, fromLocal, toLocal), TimeSpan.FromMinutes(minutes));
            }

            return ReportWidgetResult.Success(descriptor, document, generated, fromLocal, toLocal);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A report that fails shows a message in its own tile; it mustn't take the whole dashboard down.
            _logger.LogError(ex, "The dashboard widget for the report '{Report}' failed to run.", descriptor.Name);

            return ReportWidgetResult.Failed(descriptor);
        }
    }

    private sealed record CachedReportWidget(ReportDocument Document, DateTime GeneratedUtc, DateTime? From, DateTime? To);
}

/// <summary>
/// What a report dashboard widget shows, or why it shows nothing.
/// </summary>
public sealed class ReportWidgetResult
{
    private ReportWidgetResult()
    {
    }

    /// <summary>
    /// Gets how the attempt ended.
    /// </summary>
    public ReportWidgetOutcome Outcome { get; private init; }

    /// <summary>
    /// Gets the report, when there is one.
    /// </summary>
    public ReportDescriptor Report { get; private init; }

    /// <summary>
    /// Gets the document to show.
    /// </summary>
    public ReportDocument Document { get; private init; }

    /// <summary>
    /// Gets when the document was made.
    /// </summary>
    public DateTime GeneratedUtc { get; private init; }

    /// <summary>
    /// Gets the first moment of the period, in the site's time zone, or <see langword="null"/> when it has no start.
    /// </summary>
    public DateTime? From { get; private init; }

    /// <summary>
    /// Gets the last moment of the period, in the site's time zone, or <see langword="null"/> when it has no end.
    /// </summary>
    public DateTime? To { get; private init; }

    internal static ReportWidgetResult NotConfigured()
        => new() { Outcome = ReportWidgetOutcome.NotConfigured };

    internal static ReportWidgetResult Denied(ReportDescriptor report)
        => new() { Outcome = ReportWidgetOutcome.Denied, Report = report };

    internal static ReportWidgetResult Failed(ReportDescriptor report)
        => new() { Outcome = ReportWidgetOutcome.Failed, Report = report };

    internal static ReportWidgetResult Success(ReportDescriptor report, ReportDocument document, DateTime generatedUtc, DateTime? from, DateTime? to)
        => new() { Outcome = ReportWidgetOutcome.Shown, Report = report, Document = document, GeneratedUtc = generatedUtc, From = from, To = to };
}

/// <summary>
/// How a report dashboard widget ended up.
/// </summary>
public enum ReportWidgetOutcome
{
    /// <summary>
    /// The report ran, or its result was kept.
    /// </summary>
    Shown,

    /// <summary>
    /// No report is chosen, or the one chosen is gone.
    /// </summary>
    NotConfigured,

    /// <summary>
    /// The person looking may not view the report.
    /// </summary>
    Denied,

    /// <summary>
    /// The report failed to run.
    /// </summary>
    Failed,
}
