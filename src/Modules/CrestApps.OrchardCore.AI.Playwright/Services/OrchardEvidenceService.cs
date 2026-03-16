using CrestApps.OrchardCore.AI.Playwright.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using OrchardClock = OrchardCore.Modules.IClock;

namespace CrestApps.OrchardCore.AI.Playwright.Services;

/// <summary>
/// Locates a named OrchardCore admin action using 5-tier priority locator strategies.
/// Captures structured evidence (screenshots, HTML) when the action is not found.
/// </summary>
public sealed class OrchardEvidenceService : IOrchardEvidenceService
{
    // OrchardCore container selectors tried in order when capturing a container screenshot.
    private static readonly string[] _containerSelectors =
    [
        ".content-item-actions",
        "[class*='actions']",
        ".dropdown-menu.show",
        ".card-footer",
        ".card-header",
        ".sticky-top",
        "main",
        "form",
        ".content",
    ];

    private readonly OrchardClock _clock;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<OrchardEvidenceService> _logger;

    public OrchardEvidenceService(
        OrchardClock clock,
        IHostEnvironment hostEnvironment,
        ILogger<OrchardEvidenceService> logger)
    {
        _clock = clock;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task<PlaywrightElementDiagnosticsResult> FindOrchardElementWithEvidenceAsync(
        IPlaywrightSession session,
        string actionLabel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(actionLabel);

        var page = GetPage(session);
        var url = page.Url;
        var title = await page.TitleAsync().WaitAsync(cancellationToken);
        var strategies = BuildLocatorStrategies(page, actionLabel);
        var attempts = new List<string>(strategies.Count);

        _logger.LogDebug(
            "Searching for OrchardCore action '{ActionLabel}' using {StrategyCount} locator strategies for session '{SessionId}'.",
            actionLabel,
            strategies.Count,
            session.SessionId);

        foreach (var (name, locator) in strategies)
        {
            attempts.Add(name);

            try
            {
                var count = await locator.CountAsync().WaitAsync(cancellationToken);
                if (count > 0)
                {
                    _logger.LogDebug(
                        "Found OrchardCore action '{ActionLabel}' via strategy '{StrategyName}' ({Count} match(es)) for session '{SessionId}'.",
                        actionLabel,
                        name,
                        count,
                        session.SessionId);

                    return new PlaywrightElementDiagnosticsResult
                    {
                        Found = true,
                        MatchedLocator = name,
                        Url = url,
                        Title = title,
                        Attempts = attempts,
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    ex,
                    "Locator strategy '{StrategyName}' threw for action '{ActionLabel}' in session '{SessionId}'.",
                    name,
                    actionLabel,
                    session.SessionId);
            }
        }

        // All strategies exhausted — capture diagnostic evidence.
        _logger.LogInformation(
            "OrchardCore action '{ActionLabel}' not found after {StrategyCount} strategies. Capturing evidence for session '{SessionId}'.",
            actionLabel,
            strategies.Count,
            session.SessionId);

        var (pagePath, containerPath, htmlPath) = await CaptureEvidenceAsync(page, session.SessionId, cancellationToken);

        return new PlaywrightElementDiagnosticsResult
        {
            Found = false,
            Url = url,
            Title = title,
            PageScreenshotPath = pagePath,
            ContainerScreenshotPath = containerPath,
            PageHtmlPath = htmlPath,
            Attempts = attempts,
        };
    }

    // -------------------------------------------------------------------------
    // Locator strategies — OrchardCore-specific, 5 tiers in priority order
    // -------------------------------------------------------------------------

    private static IReadOnlyList<(string Name, ILocator Locator)> BuildLocatorStrategies(IPage page, string actionLabel)
    {
        var escaped = actionLabel.Replace("'", "\\'");

        return
        [
            // Tier 1: Role link — Edit / Preview appear as <a> in content list rows
            (
                $"role:link[name='{actionLabel}']",
                page.GetByRole(AriaRole.Link, new() { Name = actionLabel, Exact = true })
            ),

            // Tier 2: Role button — Publish Now / Save Draft are <button> in editor toolbars
            (
                $"role:button[name='{actionLabel}']",
                page.GetByRole(AriaRole.Button, new() { Name = actionLabel, Exact = true })
            ),

            // Tier 3: Accessible text match — catches labels rendered in spans, badges, or custom elements
            (
                $"text:exact['{actionLabel}']",
                page.GetByText(actionLabel, new() { Exact = true })
            ),

            // Tier 4: OrchardCore structural — action columns, dropdowns, primary button groups
            (
                $"orchard:actions['{actionLabel}']",
                page.Locator(
                    $".actions a:has-text('{escaped}'), " +
                    $"[class*='actions'] a:has-text('{escaped}'), " +
                    $".btn:has-text('{escaped}'), " +
                    $"[class*='btn']:has-text('{escaped}'), " +
                    $".dropdown-menu a:has-text('{escaped}')")
            ),

            // Tier 5: Broadest fallback — any <a> or <button> anywhere on the page
            (
                $"fallback:any-link-or-button['{actionLabel}']",
                page.Locator($"a:has-text('{escaped}'), button:has-text('{escaped}')")
            ),
        ];
    }

    // -------------------------------------------------------------------------
    // Evidence capture
    // -------------------------------------------------------------------------

    private async Task<(string PagePath, string ContainerPath, string HtmlPath)> CaptureEvidenceAsync(
        IPage page,
        string sessionId,
        CancellationToken cancellationToken)
    {
        var timestamp = _clock.UtcNow;
        var folder = Path.Combine(
            _hostEnvironment.ContentRootPath,
            "App_Data", "Playwright", "Evidence",
            sessionId,
            $"{timestamp:yyyyMMdd-HHmmssfff}");

        Directory.CreateDirectory(folder);

        var pagePath = await CaptureFullPageScreenshotAsync(page, folder, cancellationToken);
        var containerPath = await CaptureContainerScreenshotAsync(page, folder, cancellationToken);
        var htmlPath = await SavePageHtmlAsync(page, folder, cancellationToken);

        return (pagePath, containerPath, htmlPath);
    }

    private static async Task<string> CaptureFullPageScreenshotAsync(IPage page, string folder, CancellationToken cancellationToken)
    {
        var path = Path.Combine(folder, "page-full.png");

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = path,
            FullPage = true,
            Type = ScreenshotType.Png,
        }).WaitAsync(cancellationToken);

        return path;
    }

    private static async Task<string> CaptureContainerScreenshotAsync(IPage page, string folder, CancellationToken cancellationToken)
    {
        var container = await FindNearestContainerAsync(page, cancellationToken);
        if (container is null)
        {
            return null;
        }

        var path = Path.Combine(folder, "container.png");

        await container.ScreenshotAsync(new LocatorScreenshotOptions
        {
            Path = path,
            Type = ScreenshotType.Png,
        }).WaitAsync(cancellationToken);

        return path;
    }

    private static async Task<ILocator> FindNearestContainerAsync(IPage page, CancellationToken cancellationToken)
    {
        foreach (var selector in _containerSelectors)
        {
            try
            {
                var locator = page.Locator(selector).First;
                var count = await locator.CountAsync().WaitAsync(cancellationToken);
                if (count > 0 && await locator.IsVisibleAsync().WaitAsync(cancellationToken))
                {
                    return locator;
                }
            }
            catch
            {
                // Container selector failed — try the next one.
            }
        }

        return null;
    }

    private static async Task<string> SavePageHtmlAsync(IPage page, string folder, CancellationToken cancellationToken)
    {
        var path = Path.Combine(folder, "page.html");
        var html = await page.ContentAsync().WaitAsync(cancellationToken);
        await File.WriteAllTextAsync(path, html, cancellationToken);
        return path;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IPage GetPage(IPlaywrightSession session)
    {
        // PlaywrightSession keeps the active page on session.Page.
        // For concrete sessions that lazy-create the page, fall through to Page directly.
        return session.Page;
    }
}
