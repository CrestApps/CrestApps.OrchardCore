using System.Globalization;
using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Reports.Designer;
using CrestApps.OrchardCore.Reports.Designer.Models;
using CrestApps.OrchardCore.Reports.Designer.Services;
using CrestApps.OrchardCore.Reports.Models;
using CrestApps.OrchardCore.Reports.Services;
using CrestApps.OrchardCore.Reports.Widgets.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Reports.Widgets.Services;

/// <summary>
/// Runs the report or view built in the report builder that a dashboard widget shows, for the period the widget covers,
/// and keeps the result for the minutes the widget asks so the dashboard doesn't run it on every visit.
/// </summary>
/// <remarks>
/// A widget is shown only to a person who may open its report, checked on every visit the way the report's own page
/// checks it. A report reads its data with its owner's access, whoever views it, so its kept result is shared by the
/// people who may view it; a view reads with the viewer's access, so its kept result is kept per viewer.
/// </remarks>
public sealed class ReportDesignWidgetService
{
    private readonly ICatalog<ReportDesign> _designs;
    private readonly ICatalog<ReportView> _views;
    private readonly ReportDesignRunner _runner;
    private readonly IAuthorizationService _authorizationService;
    private readonly ReportDisplayValueResolver _displayValueResolver;
    private readonly IMemoryCache _memoryCache;
    private readonly ILocalClock _localClock;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReportDesignWidgetService"/> class.
    /// </summary>
    /// <param name="designs">The published reports.</param>
    /// <param name="views">The reusable views.</param>
    /// <param name="runner">The runner of designed reports.</param>
    /// <param name="authorizationService">The authorization service.</param>
    /// <param name="displayValueResolver">The resolver of typed report values.</param>
    /// <param name="memoryCache">The cache that keeps what the widgets show.</param>
    /// <param name="localClock">The clock of the site's time zone.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public ReportDesignWidgetService(
        ICatalog<ReportDesign> designs,
        ICatalog<ReportView> views,
        ReportDesignRunner runner,
        IAuthorizationService authorizationService,
        ReportDisplayValueResolver displayValueResolver,
        IMemoryCache memoryCache,
        ILocalClock localClock,
        IClock clock,
        ILogger<ReportDesignWidgetService> logger)
    {
        _designs = designs;
        _views = views;
        _runner = runner;
        _authorizationService = authorizationService;
        _displayValueResolver = displayValueResolver;
        _memoryCache = memoryCache;
        _localClock = localClock;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Gets what the widget shows.
    /// </summary>
    /// <param name="part">The widget.</param>
    /// <param name="user">The person looking at the dashboard.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The outcome, which says why when there is nothing to show.</returns>
    public async Task<ReportDesignWidgetResult> GetAsync(ReportDesignWidgetPart part, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(part);

        var source = await FindSourceAsync(part, cancellationToken);

        if (source is null)
        {
            return ReportDesignWidgetResult.NotConfigured(!string.IsNullOrEmpty(part.ReportId) || !string.IsNullOrEmpty(part.ViewId));
        }

        // The same check as the report's own page, on every visit: the kept result is never shown to a person who may
        // not open the report.
        if (user is null || !await _authorizationService.AuthorizeAsync(user, ReportDesignerPermissions.ViewAllReportDesigns, source.Resource))
        {
            return ReportDesignWidgetResult.Denied(source.IsView);
        }

        var period = Enum.IsDefined(part.Period) ? part.Period : ReportDesignWidgetPart.DefaultPeriod;
        var content = source.IsView
            ? ReportWidgetContent.TablesOnly
            : Enum.IsDefined(part.Show) ? part.Show : ReportDesignWidgetPart.DefaultContent;
        var maxRows = Math.Clamp(part.MaxRows, 0, ReportWidgetChoices.MaxRows);
        var minutes = Math.Clamp(part.CacheMinutes, 0, ReportWidgetChoices.MaxCacheMinutes);

        // A report reads its data with its owner's access, so everyone who may open it sees the same result; a view
        // reads with the viewer's access, so each viewer has their own.
        var reader = source.IsView ? $"viewer:{UserKey(user)}" : $"owner:{source.Design.OwnerId}";
        var culture = CultureInfo.CurrentUICulture.Name;
        var key = $"CrestApps.ReportDesignWidget:{part.ContentItem?.ContentItemId}:{part.ContentItem?.ModifiedUtc?.Ticks}:{(source.IsView ? "view" : "report")}:{source.Design.ItemId}:{source.ModifiedUtc?.Ticks}:{reader}:{period}:{content}:{maxRows}:{culture}";

        if (minutes > 0 && _memoryCache.TryGetValue(key, out CachedDesignWidget cached))
        {
            return ReportDesignWidgetResult.Success(source.Design, source.IsView, cached.Document, cached.GeneratedUtc, cached.From, cached.To, cached.PeriodApplied, cached.Warnings);
        }

        try
        {
            var localNow = (await _localClock.ConvertToLocalAsync(_clock.UtcNow)).DateTime;
            var (from, to) = ReportWidgetPeriods.Resolve(period, localNow, part.From, part.To);
            var runPeriod = ReportRunPeriod.FromRange(from, to);
            var run = source.IsView
                ? await _runner.RunAsync(source.Design, user, filterValues: null, period: runPeriod, cancellationToken: cancellationToken)
                : await _runner.RunAsync(source.Design, filterValues: null, period: runPeriod, cancellationToken: cancellationToken);

            if (run.Document is null)
            {
                _logger.LogWarning(
                    "The dashboard widget for the {Kind} '{ItemId}' couldn't run it: {Errors}",
                    source.IsView ? "view" : "report",
                    source.Design.ItemId,
                    string.Join(" ", run.Errors));

                return ReportDesignWidgetResult.Failed(source.Design, source.IsView, [.. run.Errors]);
            }

            await _displayValueResolver.ResolveNonTableValuesAsync(run.Document);
            ReportWidgetTrimmer.Trim(run.Document, content, maxRows);

            var generated = _clock.UtcNow;
            var periodApplied = run.PeriodFilterId is not null;
            IReadOnlyList<string> warnings = [.. run.Warnings];

            if (minutes > 0)
            {
                _memoryCache.Set(key, new CachedDesignWidget(run.Document, generated, from, to, periodApplied, warnings), TimeSpan.FromMinutes(minutes));
            }

            return ReportDesignWidgetResult.Success(source.Design, source.IsView, run.Document, generated, from, to, periodApplied, warnings);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A report that fails shows a message in its own tile; it mustn't take the whole dashboard down.
            _logger.LogError(ex, "The dashboard widget for the {Kind} '{ItemId}' failed to run it.", source.IsView ? "view" : "report", source.Design.ItemId);

            return ReportDesignWidgetResult.Failed(source.Design, source.IsView, []);
        }
    }

    private async Task<WidgetSource> FindSourceAsync(ReportDesignWidgetPart part, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(part.ReportId))
        {
            // Only published reports are in the catalog: a report that was never published has nothing to show.
            var design = await _designs.FindByIdAsync(part.ReportId, cancellationToken);

            return design is null ? null : new WidgetSource(design, design, false, design.ModifiedUtc);
        }

        if (!string.IsNullOrEmpty(part.ViewId))
        {
            var view = await _views.FindByIdAsync(part.ViewId, cancellationToken);

            if (view is null)
            {
                return null;
            }

            // A view runs like a report with no visuals, which shows its result as one table.
            var design = new ReportDesign
            {
                ItemId = view.ItemId,
                DisplayText = view.DisplayText,
                Description = view.Description,
                Query = view.Query,
                Visuals = [],
                OwnerId = view.OwnerId,
                ModifiedUtc = view.ModifiedUtc,
            };

            return new WidgetSource(design, view, true, view.ModifiedUtc);
        }

        return null;
    }

    private static string UserKey(ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.Identity?.Name ?? string.Empty;
    }

    // What the widget shows: the report to run, the resource its permission is checked against, and when it changed.
    private sealed record WidgetSource(ReportDesign Design, object Resource, bool IsView, DateTime? ModifiedUtc);

    private sealed record CachedDesignWidget(
        ReportDocument Document,
        DateTime GeneratedUtc,
        DateTime? From,
        DateTime? To,
        bool PeriodApplied,
        IReadOnlyList<string> Warnings);
}

/// <summary>
/// What a dashboard widget of a report built in the report builder shows, or why it shows nothing.
/// </summary>
public sealed class ReportDesignWidgetResult
{
    private ReportDesignWidgetResult()
    {
    }

    /// <summary>
    /// Gets how the attempt ended.
    /// </summary>
    public ReportWidgetOutcome Outcome { get; private init; }

    /// <summary>
    /// Gets a value indicating whether the widget names a report or view that is gone, or a report that is not
    /// published.
    /// </summary>
    public bool IsMissing { get; private init; }

    /// <summary>
    /// Gets a value indicating whether the widget shows a reusable view rather than a report.
    /// </summary>
    public bool IsView { get; private init; }

    /// <summary>
    /// Gets the identifier of the report or view, when the person may see it.
    /// </summary>
    public string ItemId { get; private init; }

    /// <summary>
    /// Gets the title of the report or view, when the person may see it.
    /// </summary>
    public string Title { get; private init; }

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

    /// <summary>
    /// Gets a value indicating whether the period was set on the report: <see langword="false"/> when the report lets
    /// its viewers pick no date range, so it shows its own dates.
    /// </summary>
    public bool PeriodApplied { get; private init; }

    /// <summary>
    /// Gets the problems that stopped the report from running, which its own page would show too.
    /// </summary>
    public IReadOnlyList<string> Errors { get; private init; } = [];

    /// <summary>
    /// Gets the problems that did not stop the report, such as data that was cut off at a limit.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; private init; } = [];

    internal static ReportDesignWidgetResult NotConfigured(bool isMissing)
        => new() { Outcome = ReportWidgetOutcome.NotConfigured, IsMissing = isMissing };

    internal static ReportDesignWidgetResult Denied(bool isView)
        => new() { Outcome = ReportWidgetOutcome.Denied, IsView = isView };

    internal static ReportDesignWidgetResult Failed(ReportDesign design, bool isView, IReadOnlyList<string> errors)
        => new() { Outcome = ReportWidgetOutcome.Failed, IsView = isView, ItemId = design.ItemId, Title = design.DisplayText, Errors = errors };

    internal static ReportDesignWidgetResult Success(
        ReportDesign design,
        bool isView,
        ReportDocument document,
        DateTime generatedUtc,
        DateTime? from,
        DateTime? to,
        bool periodApplied,
        IReadOnlyList<string> warnings)
    {
        return new()
        {
            Outcome = ReportWidgetOutcome.Shown,
            IsView = isView,
            ItemId = design.ItemId,
            Title = design.DisplayText,
            Document = document,
            GeneratedUtc = generatedUtc,
            From = from,
            To = to,
            PeriodApplied = periodApplied,
            Warnings = warnings,
        };
    }
}
