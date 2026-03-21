using System.Text.RegularExpressions;
using CrestApps.OrchardCore.AI.Playwright.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using OrchardClock = OrchardCore.Modules.IClock;

namespace CrestApps.OrchardCore.AI.Playwright.Services;

public sealed class PlaywrightPageInspectionService : IPlaywrightPageInspectionService
{
    private static readonly Regex _whitespace = new(@"\s{2,}", RegexOptions.Compiled);

    private readonly OrchardClock _clock;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<PlaywrightPageInspectionService> _logger;

    public PlaywrightPageInspectionService(
        OrchardClock clock,
        IHostEnvironment hostEnvironment,
        ILogger<PlaywrightPageInspectionService> logger)
    {
        _clock = clock;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task<PlaywrightPageContentResult> GetPageContentAsync(IPlaywrightSession session, int maxLength, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var page = await GetActivePageAsync(session, cancellationToken);
        maxLength = Math.Clamp(maxLength, 500, 16_000);

        _logger.LogDebug("Inspecting visible page content for Playwright session '{ChatSessionId}'.", session.SessionId);

        var payload = await page.EvaluateAsync<PageContentPayload>(
            """
            (maxLength) => {
                const normalize = (value) => (value || '').replace(/\s+/g, ' ').trim();
                const body = document.body ? document.body.cloneNode(true) : null;

                if (body) {
                    body.querySelectorAll('script, style, noscript, [hidden], [aria-hidden="true"]').forEach(element => element.remove());
                }

                const heading = normalize(
                    document.querySelector('main h1, .page-title h1, .page-title, h1')?.innerText || ''
                );

                let content = normalize(body?.innerText || '');
                if (content.length > maxLength) {
                    content = `${content.slice(0, maxLength)}\n\n[...truncated - ${content.length - maxLength} more characters]`;
                }

                return {
                    mainHeading: heading,
                    content
                };
            }
            """,
            maxLength).WaitAsync(cancellationToken);

        return new PlaywrightPageContentResult
        {
            Url = page.Url,
            Title = await page.TitleAsync().WaitAsync(cancellationToken),
            MainHeading = NormalizeText(payload?.MainHeading),
            Content = NormalizeText(payload?.Content),
        };
    }

    public async Task<PlaywrightElementSearchResult> FindElementsAsync(IPlaywrightSession session, string query, int maxMatches, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var page = await GetActivePageAsync(session, cancellationToken);
        maxMatches = Math.Clamp(maxMatches, 1, 12);

        _logger.LogDebug(
            "Finding visible elements matching '{Query}' for Playwright session '{ChatSessionId}'.",
            query,
            session.SessionId);

        var matches = await page.EvaluateAsync<ElementMatchPayload[]>(
            """
            (args) => {
                const normalize = (value) => (value || '').replace(/\s+/g, ' ').trim();
                const isVisible = (element) => {
                    if (!(element instanceof HTMLElement)) {
                        return false;
                    }

                    if (element.hidden) {
                        return false;
                    }

                    const style = window.getComputedStyle(element);
                    if (style.display === 'none' || style.visibility === 'hidden' || style.opacity === '0') {
                        return false;
                    }

                    return element.offsetWidth > 0 || element.offsetHeight > 0 || element.getClientRects().length > 0;
                };

                const query = normalize(args.query).toLowerCase();
                const maxMatches = args.maxMatches;
                const results = [];
                const seen = new Set();

                for (const element of Array.from(document.querySelectorAll('body *'))) {
                    if (!isVisible(element)) {
                        continue;
                    }

                    const text = normalize(element.innerText || element.textContent || '');
                    const label = normalize(Array.from(element.labels || []).map(labelElement => labelElement.innerText).join(' | '));
                    const role = normalize(element.getAttribute('role') || '');
                    const title = normalize(element.getAttribute('title') || '');
                    const placeholder = normalize(element.getAttribute('placeholder') || '');
                    const name = normalize(element.getAttribute('name') || '');
                    const id = normalize(element.id || '');
                    const widgetType = normalize(element.getAttribute('data-widget-type') || element.getAttribute('data-widget') || '');
                    const ariaLabel = normalize(element.getAttribute('aria-label') || '');

                    const searchable = [text, label, role, title, placeholder, name, id, widgetType, ariaLabel]
                        .filter(Boolean)
                        .join(' | ')
                        .toLowerCase();

                    if (!searchable.includes(query)) {
                        continue;
                    }

                    const selectorHint = id
                        ? `#${id}`
                        : name
                            ? `[name="${name}"]`
                            : ariaLabel
                                ? `[aria-label="${ariaLabel}"]`
                                : text
                                    ? `text=${text.slice(0, 80)}`
                                    : element.tagName.toLowerCase();

                    const key = `${element.tagName}|${id}|${name}|${selectorHint}|${text.slice(0, 80)}`.toLowerCase();
                    if (seen.has(key)) {
                        continue;
                    }

                    seen.add(key);
                    results.push({
                        tagName: element.tagName.toLowerCase(),
                        role,
                        text: text.slice(0, 220),
                        label,
                        id,
                        name,
                        title,
                        placeholder,
                        widgetType,
                        selectorHint
                    });

                    if (results.length >= maxMatches) {
                        break;
                    }
                }

                return results;
            }
            """,
            new
            {
                query,
                maxMatches,
            }).WaitAsync(cancellationToken);

        var normalizedMatches = (matches ?? [])
            .Select(match => new PlaywrightElementMatch
            {
                TagName = NormalizeText(match.TagName),
                Role = NormalizeText(match.Role),
                Text = NormalizeText(match.Text),
                Label = NormalizeText(match.Label),
                Id = NormalizeText(match.Id),
                Name = NormalizeText(match.Name),
                Title = NormalizeText(match.Title),
                Placeholder = NormalizeText(match.Placeholder),
                WidgetType = NormalizeText(match.WidgetType),
                SelectorHint = NormalizeText(match.SelectorHint),
            })
            .ToList();

        return new PlaywrightElementSearchResult
        {
            Query = query,
            Url = page.Url,
            Title = await page.TitleAsync().WaitAsync(cancellationToken),
            Exists = normalizedMatches.Count > 0,
            MatchCount = normalizedMatches.Count,
            Matches = normalizedMatches,
        };
    }

    public Task<PlaywrightElementSearchResult> CheckElementExistsAsync(IPlaywrightSession session, string query, int maxMatches, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Checking whether '{Query}' exists on the page for Playwright session '{ChatSessionId}'.",
            query,
            session?.SessionId);

        return FindElementsAsync(session, query, maxMatches, cancellationToken);
    }

    public async Task<PlaywrightVisibleWidgetsResult> GetVisibleWidgetsAsync(IPlaywrightSession session, int maxItems, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var page = await GetActivePageAsync(session, cancellationToken);
        maxItems = Math.Clamp(maxItems, 1, 20);

        _logger.LogDebug("Listing visible widgets for Playwright session '{ChatSessionId}'.", session.SessionId);

        var widgets = await page.EvaluateAsync<VisibleWidgetPayload[]>(
            """
            (maxItems) => {
                const normalize = (value) => (value || '').replace(/\s+/g, ' ').trim();
                const isVisible = (element) => {
                    if (!(element instanceof HTMLElement)) {
                        return false;
                    }

                    if (element.hidden) {
                        return false;
                    }

                    const style = window.getComputedStyle(element);
                    if (style.display === 'none' || style.visibility === 'hidden' || style.opacity === '0') {
                        return false;
                    }

                    return element.offsetWidth > 0 || element.offsetHeight > 0 || element.getClientRects().length > 0;
                };

                const selectors = [
                    '[data-widget-type]',
                    '[data-widget]',
                    '[class*="widget"]',
                    '.widget',
                    '.card-title',
                    '.card-header',
                    'legend',
                    'summary',
                    'h1',
                    'h2',
                    'h3',
                    'h4',
                    'h5',
                    'h6'
                ];

                const seen = new Set();
                const results = [];

                for (const element of Array.from(document.querySelectorAll(selectors.join(',')))) {
                    if (!isVisible(element)) {
                        continue;
                    }

                    const name = normalize(
                        element.getAttribute('data-widget-type')
                        || element.getAttribute('data-widget')
                        || element.innerText
                        || element.textContent
                        || ''
                    ).slice(0, 140);

                    if (!name || name.length < 2) {
                        continue;
                    }

                    const source = normalize(
                        element.getAttribute('data-widget-type')
                            ? 'data-widget-type'
                            : element.getAttribute('data-widget')
                                ? 'data-widget'
                                : element.tagName.toLowerCase()
                    );

                    const key = `${source}|${name}`.toLowerCase();
                    if (seen.has(key)) {
                        continue;
                    }

                    seen.add(key);
                    results.push({ name, source });

                    if (results.length >= maxItems) {
                        break;
                    }
                }

                return results;
            }
            """,
            maxItems).WaitAsync(cancellationToken);

        var normalizedWidgets = (widgets ?? [])
            .Select(widget => new PlaywrightVisibleWidget
            {
                Name = NormalizeText(widget.Name),
                Source = NormalizeText(widget.Source),
            })
            .Where(widget => !string.IsNullOrWhiteSpace(widget.Name))
            .ToList();

        return new PlaywrightVisibleWidgetsResult
        {
            Url = page.Url,
            Title = await page.TitleAsync().WaitAsync(cancellationToken),
            WidgetCount = normalizedWidgets.Count,
            Widgets = normalizedWidgets,
        };
    }

    public async Task<PlaywrightScreenshotResult> TakeScreenshotAsync(IPlaywrightSession session, bool fullPage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var page = await GetActivePageAsync(session, cancellationToken);
        var takenAtUtc = _clock.UtcNow;
        var directory = Path.Combine(_hostEnvironment.ContentRootPath, "App_Data", "Playwright", "Screenshots", session.SessionId);
        Directory.CreateDirectory(directory);

        var fileName = $"{takenAtUtc:yyyyMMdd-HHmmssfff}.png";
        var savedPath = Path.Combine(directory, fileName);

        _logger.LogDebug(
            "Capturing screenshot for Playwright session '{ChatSessionId}' to '{SavedPath}'.",
            session.SessionId,
            savedPath);

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = savedPath,
            FullPage = fullPage,
            Type = ScreenshotType.Png,
        }).WaitAsync(cancellationToken);

        return new PlaywrightScreenshotResult
        {
            Url = page.Url,
            Title = await page.TitleAsync().WaitAsync(cancellationToken),
            SavedPath = savedPath,
            FullPage = fullPage,
            TakenAtUtc = takenAtUtc,
        };
    }

    private static async Task<IPage> GetActivePageAsync(IPlaywrightSession session, CancellationToken cancellationToken)
    {
        return session switch
        {
            PlaywrightSession concreteSession => await concreteSession.GetOrCreatePageAsync(cancellationToken),
            _ => session.Page,
        };
    }

    private static string NormalizeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return _whitespace.Replace(value.Trim(), " ");
    }

    private sealed class PageContentPayload
    {
        public string MainHeading { get; set; }

        public string Content { get; set; }
    }

    private sealed class ElementMatchPayload
    {
        public string TagName { get; set; }

        public string Role { get; set; }

        public string Text { get; set; }

        public string Label { get; set; }

        public string Id { get; set; }

        public string Name { get; set; }

        public string Title { get; set; }

        public string Placeholder { get; set; }

        public string WidgetType { get; set; }

        public string SelectorHint { get; set; }
    }

    private sealed class VisibleWidgetPayload
    {
        public string Name { get; set; }

        public string Source { get; set; }
    }
}
