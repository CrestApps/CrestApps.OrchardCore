using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Agent.Services;

public sealed class BrowserAutomationService : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, BrowserAutomationSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly global::OrchardCore.Modules.IClock _clock;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<BrowserAutomationService> _logger;

    public BrowserAutomationService(
        global::OrchardCore.Modules.IClock clock,
        IHttpContextAccessor httpContextAccessor,
        ILogger<BrowserAutomationService> logger)
    {
        _clock = clock;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Dictionary<string, object>>> ListSessionsAsync(CancellationToken cancellationToken)
    {
        await CleanupExpiredSessionsAsync(cancellationToken);

        var snapshots = new List<Dictionary<string, object>>();

        foreach (var sessionId in _sessions.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            snapshots.Add(await GetSessionSnapshotAsync(sessionId, cancellationToken));
        }

        return snapshots;
    }

    public async Task<Dictionary<string, object>> GetSessionSnapshotAsync(string sessionId, CancellationToken cancellationToken)
    {
        return await WithSessionAsync(sessionId, BuildSessionSnapshotAsync, cancellationToken);
    }

    public async Task<Dictionary<string, object>> CreateSessionAsync(
        string browserType,
        bool headless,
        string startUrl,
        int? viewportWidth,
        int? viewportHeight,
        string locale,
        string userAgent,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        await CleanupExpiredSessionsAsync(cancellationToken);

        browserType = NormalizeBrowserType(browserType);

        var createdUtc = _clock.UtcNow;
        var sessionId = Guid.NewGuid().ToString("n");
        var playwright = await Playwright.CreateAsync();
        IBrowser browser = null;
        IBrowserContext context = null;

        try
        {
            browser = await LaunchBrowserAsync(playwright, browserType, headless, timeoutMs);

            var contextOptions = new BrowserNewContextOptions();
            if (viewportWidth.HasValue && viewportHeight.HasValue)
            {
                contextOptions.ViewportSize = new ViewportSize
                {
                    Width = viewportWidth.Value,
                    Height = viewportHeight.Value,
                };
            }

            if (!string.IsNullOrWhiteSpace(locale))
            {
                contextOptions.Locale = locale.Trim();
            }

            if (!string.IsNullOrWhiteSpace(userAgent))
            {
                contextOptions.UserAgent = userAgent.Trim();
            }

            context = await browser.NewContextAsync(contextOptions);
            await CopyCurrentRequestCookiesAsync(context, startUrl);

            var session = new BrowserAutomationSession(sessionId, browserType, headless, playwright, browser, context, createdUtc);
            _sessions[sessionId] = session;

            var page = await context.NewPageAsync();
            var trackedPage = TrackPage(session, page);

            if (!string.IsNullOrWhiteSpace(startUrl))
            {
                await page.GotoAsync(startUrl.Trim(), new PageGotoOptions
                {
                    Timeout = timeoutMs,
                    WaitUntil = WaitUntilState.Load,
                });
            }

            return await BuildSessionSnapshotAsync(session);
        }
        catch (PlaywrightException) when (context is not null || browser is not null)
        {
            if (context is not null)
            {
                await context.CloseAsync();
            }

            if (browser is not null)
            {
                await browser.CloseAsync();
            }

            playwright.Dispose();
            _sessions.TryRemove(sessionId, out _);
            throw;
        }
    }

    public async Task<Dictionary<string, object>> CreatePageAsync(
        string sessionId,
        string url,
        WaitUntilState waitUntil,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        return await WithSessionAsync(sessionId, async session =>
        {
            var page = await session.Context.NewPageAsync();
            var trackedPage = TrackPage(session, page);

            if (!string.IsNullOrWhiteSpace(url))
            {
                await page.GotoAsync(url.Trim(), new PageGotoOptions
                {
                    Timeout = timeoutMs,
                    WaitUntil = waitUntil,
                });
            }

            return await BuildPageSnapshotAsync(session, trackedPage);
        }, cancellationToken);
    }

    public async Task<Dictionary<string, object>> SwitchActivePageAsync(string sessionId, string pageId, CancellationToken cancellationToken)
    {
        return await WithSessionAsync(sessionId, async session =>
        {
            if (!session.Pages.TryGetValue(pageId, out var trackedPage))
            {
                throw new InvalidOperationException($"Page '{pageId}' was not found for session '{sessionId}'.");
            }

            session.ActivePageId = trackedPage.PageId;
            trackedPage.Touch(_clock.UtcNow);
            return await BuildPageSnapshotAsync(session, trackedPage);
        }, cancellationToken);
    }

    public async Task<Dictionary<string, object>> ClosePageAsync(string sessionId, string pageId, CancellationToken cancellationToken)
    {
        return await WithSessionAsync(sessionId, async session =>
        {
            var trackedPage = await ResolvePageAsync(session, pageId, cancellationToken);
            var snapshot = await BuildPageSnapshotAsync(session, trackedPage);

            session.Pages.TryRemove(trackedPage.PageId, out _);

            if (!trackedPage.Page.IsClosed)
            {
                await trackedPage.Page.CloseAsync();
            }

            session.ActivePageId = session.Pages.Values
                .OrderByDescending(x => x.LastTouchedUtc)
                .Select(x => x.PageId)
                .FirstOrDefault();

            return snapshot;
        }, cancellationToken);
    }

    public async Task<Dictionary<string, object>> CloseSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        await CleanupExpiredSessionsAsync(cancellationToken);

        var resolvedSessionId = ResolveRequestedSessionId(sessionId, _sessions.Values);
        if (!_sessions.TryRemove(resolvedSessionId, out var session))
        {
            throw new InvalidOperationException($"Browser session '{resolvedSessionId}' was not found.");
        }

        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = await BuildSessionSnapshotAsync(session);

            foreach (var trackedPage in session.Pages.Values)
            {
                if (!trackedPage.Page.IsClosed)
                {
                    await trackedPage.Page.CloseAsync();
                }
            }

            await session.Context.CloseAsync();
            await session.Browser.CloseAsync();
            session.Playwright.Dispose();

            return snapshot;
        }
        finally
        {
            session.Gate.Release();
            session.Gate.Dispose();
        }
    }

    internal async Task<TResult> WithSessionAsync<TResult>(
        string sessionId,
        Func<BrowserAutomationSession, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        await CleanupExpiredSessionsAsync(cancellationToken);

        var resolvedSessionId = await ResolveRequestedSessionIdAsync(sessionId, cancellationToken);

        if (!_sessions.TryGetValue(resolvedSessionId, out var session))
        {
            throw new InvalidOperationException($"Browser session '{resolvedSessionId}' was not found.");
        }

        await session.Gate.WaitAsync(cancellationToken);
        try
        {
            session.Touch(_clock.UtcNow);
            return await action(session);
        }
        finally
        {
            session.Gate.Release();
        }
    }

    internal static string ResolveBootstrapUrl(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (TryGetAbsoluteHttpUrl(httpContext.Request.Query[AgentConstants.BrowserParentPageUrlQueryKey], out var parentPageUrl))
        {
            return parentPageUrl;
        }

        if (TryGetAbsoluteHttpUrl(httpContext.Request.Query[AgentConstants.BrowserPageUrlQueryKey], out var pageUrl))
        {
            return pageUrl;
        }

        if (TryGetAbsoluteHttpUrl(httpContext.Request.Headers.Referer, out var refererUrl))
        {
            return refererUrl;
        }

        return null;
    }

    internal static string ResolveRequestedSessionId(string sessionId, IEnumerable<BrowserAutomationSession> sessions)
    {
        var normalizedSessionId = string.IsNullOrWhiteSpace(sessionId)
            ? AgentConstants.DefaultSessionId
            : sessionId.Trim();

        if (!string.Equals(normalizedSessionId, AgentConstants.DefaultSessionId, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedSessionId;
        }

        var resolvedSessionId = sessions
            .OrderByDescending(session => session.LastTouchedUtc)
            .Select(session => session.SessionId)
            .FirstOrDefault();

        if (resolvedSessionId is null)
        {
            throw new InvalidOperationException($"No active browser session was found. Call 'startBrowserSession' first, then reuse the returned sessionId or use the '{AgentConstants.DefaultSessionId}' alias.");
        }

        return resolvedSessionId;
    }

    internal async Task<TResult> WithPageAsync<TResult>(
        string sessionId,
        string pageId,
        Func<BrowserAutomationSession, BrowserAutomationPage, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        return await WithSessionAsync(sessionId, async session =>
        {
            var trackedPage = await ResolvePageAsync(session, pageId, cancellationToken);
            trackedPage.Touch(_clock.UtcNow);
            session.ActivePageId = trackedPage.PageId;
            return await action(session, trackedPage);
        }, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sessionId in _sessions.Keys.ToArray())
        {
            await CloseSessionAsync(sessionId, CancellationToken.None);
        }
    }

    private async Task CleanupExpiredSessionsAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var expirationCutoff = now - AgentConstants.SessionIdleTimeout;
        var expiredSessionIds = _sessions.Values
            .Where(x => x.LastTouchedUtc < expirationCutoff)
            .Select(x => x.SessionId)
            .ToArray();

        foreach (var sessionId in expiredSessionIds)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Closing expired browser automation session '{SessionId}'.", sessionId);
            }

            await CloseSessionAsync(sessionId, cancellationToken);
        }
    }

    private async Task<string> ResolveRequestedSessionIdAsync(string sessionId, CancellationToken cancellationToken)
    {
        var normalizedSessionId = string.IsNullOrWhiteSpace(sessionId)
            ? AgentConstants.DefaultSessionId
            : sessionId.Trim();

        if (!string.Equals(normalizedSessionId, AgentConstants.DefaultSessionId, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedSessionId;
        }

        var existingSessionId = _sessions.Values
            .OrderByDescending(session => session.LastTouchedUtc)
            .Select(session => session.SessionId)
            .FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(existingSessionId))
        {
            return existingSessionId;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            throw new InvalidOperationException($"No active browser session was found. Call 'startBrowserSession' first, then reuse the returned sessionId or use the '{AgentConstants.DefaultSessionId}' alias.");
        }

        var bootstrapUrl = ResolveBootstrapUrl(httpContext);
        if (string.IsNullOrWhiteSpace(bootstrapUrl))
        {
            throw new InvalidOperationException($"No active browser session was found. Call 'startBrowserSession' first, then reuse the returned sessionId or use the '{AgentConstants.DefaultSessionId}' alias.");
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Auto-starting browser automation session for '{StartUrl}' because no active session was found for the default alias.", bootstrapUrl);
        }

        var snapshot = await CreateSessionAsync(
            "chromium",
            true,
            bootstrapUrl,
            null,
            null,
            null,
            null,
            AgentConstants.DefaultTimeoutMs,
            cancellationToken);

        if (!snapshot.TryGetValue("sessionId", out var sessionIdValue) ||
            sessionIdValue is not string createdSessionId ||
            string.IsNullOrWhiteSpace(createdSessionId))
        {
            throw new InvalidOperationException("The browser automation session was created, but its session identifier was missing from the snapshot.");
        }

        return createdSessionId;
    }

    private static async Task<IBrowser> LaunchBrowserAsync(IPlaywright playwright, string browserType, bool headless, int timeoutMs)
    {
        var options = new BrowserTypeLaunchOptions
        {
            Headless = headless,
            Timeout = timeoutMs,
        };

        return browserType switch
        {
            "chromium" => await playwright.Chromium.LaunchAsync(options),
            "firefox" => await playwright.Firefox.LaunchAsync(options),
            "webkit" => await playwright.Webkit.LaunchAsync(options),
            _ => throw new InvalidOperationException($"Unsupported browser type '{browserType}'. Supported values are chromium, firefox, and webkit."),
        };
    }

    private async Task CopyCurrentRequestCookiesAsync(IBrowserContext context, string startUrl)
    {
        if (string.IsNullOrWhiteSpace(startUrl))
        {
            return;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        if (!Uri.TryCreate(startUrl.Trim(), UriKind.Absolute, out var startUri) ||
            !string.Equals(startUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(startUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var request = httpContext.Request;
        if (request.Cookies.Count == 0)
        {
            return;
        }

        if (!string.Equals(request.Host.Host, startUri.Host, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var requestPort = request.Host.Port ?? (string.Equals(request.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80);
        var startPort = startUri.IsDefaultPort ? (string.Equals(startUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80) : startUri.Port;
        if (!string.Equals(request.Scheme, startUri.Scheme, StringComparison.OrdinalIgnoreCase) || requestPort != startPort)
        {
            return;
        }

        var cookieUrl = startUri.GetLeftPart(UriPartial.Authority);
        var cookies = request.Cookies
            .Select(entry => new Cookie
            {
                Name = entry.Key,
                Value = entry.Value,
                Url = cookieUrl,
                Secure = string.Equals(startUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase),
            })
            .ToArray();

        if (cookies.Length == 0)
        {
            return;
        }

        await context.AddCookiesAsync(cookies);
    }

    private static bool TryGetAbsoluteHttpUrl(string value, out string normalizedUrl)
    {
        normalizedUrl = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        normalizedUrl = uri.ToString();
        return true;
    }

    private BrowserAutomationPage TrackPage(BrowserAutomationSession session, IPage page)
    {
        var pageId = $"page-{Interlocked.Increment(ref session.PageSequence)}";
        var trackedPage = new BrowserAutomationPage(pageId, page, _clock.UtcNow);
        session.Pages[pageId] = trackedPage;
        session.ActivePageId = pageId;

        page.Console += (_, message) =>
        {
            var consoleEntry = new Dictionary<string, object>
            {
                ["pageId"] = pageId,
                ["type"] = message.Type,
                ["text"] = message.Text,
                ["timestampUtc"] = _clock.UtcNow,
            };

            EnqueueLimited(trackedPage.ConsoleMessages, consoleEntry, AgentConstants.MaxStoredConsoleMessages);
        };

        page.PageError += (_, error) =>
        {
            EnqueueLimited(trackedPage.PageErrors, error, AgentConstants.MaxStoredConsoleMessages);
            EnqueueLimited(trackedPage.ConsoleMessages, new Dictionary<string, object>
            {
                ["pageId"] = pageId,
                ["type"] = "pageerror",
                ["text"] = error,
                ["timestampUtc"] = _clock.UtcNow,
            }, AgentConstants.MaxStoredConsoleMessages);
        };

        page.Request += (_, request) =>
        {
            EnqueueLimited(trackedPage.NetworkEvents, new Dictionary<string, object>
            {
                ["pageId"] = pageId,
                ["phase"] = "request",
                ["method"] = request.Method,
                ["url"] = request.Url,
                ["resourceType"] = request.ResourceType,
                ["timestampUtc"] = _clock.UtcNow,
            }, AgentConstants.MaxStoredNetworkEvents);
        };

        page.Response += (_, response) =>
        {
            EnqueueLimited(trackedPage.NetworkEvents, new Dictionary<string, object>
            {
                ["pageId"] = pageId,
                ["phase"] = "response",
                ["url"] = response.Url,
                ["status"] = response.Status,
                ["ok"] = response.Ok,
                ["timestampUtc"] = _clock.UtcNow,
            }, AgentConstants.MaxStoredNetworkEvents);
        };

        page.RequestFailed += (_, request) =>
        {
            EnqueueLimited(trackedPage.NetworkEvents, new Dictionary<string, object>
            {
                ["pageId"] = pageId,
                ["phase"] = "requestfailed",
                ["method"] = request.Method,
                ["url"] = request.Url,
                ["resourceType"] = request.ResourceType,
                ["timestampUtc"] = _clock.UtcNow,
            }, AgentConstants.MaxStoredNetworkEvents);
        };

        return trackedPage;
    }

    private async Task<BrowserAutomationPage> ResolvePageAsync(
        BrowserAutomationSession session,
        string pageId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(pageId) && session.Pages.TryGetValue(pageId, out var explicitPage))
        {
            return explicitPage;
        }

        if (!string.IsNullOrWhiteSpace(pageId))
        {
            throw new InvalidOperationException($"Page '{pageId}' was not found for session '{session.SessionId}'.");
        }

        if (!string.IsNullOrWhiteSpace(session.ActivePageId) && session.Pages.TryGetValue(session.ActivePageId, out var activePage))
        {
            return activePage;
        }

        var page = await session.Context.NewPageAsync();
        return TrackPage(session, page);
    }

    private async Task<Dictionary<string, object>> BuildSessionSnapshotAsync(BrowserAutomationSession session)
    {
        var pages = new List<Dictionary<string, object>>();

        foreach (var trackedPage in session.Pages.Values.OrderBy(x => x.CreatedUtc))
        {
            pages.Add(await BuildPageSnapshotAsync(session, trackedPage));
        }

        return new Dictionary<string, object>
        {
            ["sessionId"] = session.SessionId,
            ["browserType"] = session.BrowserType,
            ["headless"] = session.Headless,
            ["createdUtc"] = session.CreatedUtc,
            ["lastTouchedUtc"] = session.LastTouchedUtc,
            ["activePageId"] = session.ActivePageId ?? string.Empty,
            ["pageCount"] = pages.Count,
            ["pages"] = pages,
        };
    }

    private static async Task<Dictionary<string, object>> BuildPageSnapshotAsync(BrowserAutomationSession session, BrowserAutomationPage trackedPage)
    {
        var snapshot = new Dictionary<string, object>
        {
            ["sessionId"] = session.SessionId,
            ["pageId"] = trackedPage.PageId,
            ["isActive"] = string.Equals(session.ActivePageId, trackedPage.PageId, StringComparison.OrdinalIgnoreCase),
            ["isClosed"] = trackedPage.Page.IsClosed,
            ["url"] = trackedPage.Page.Url ?? string.Empty,
            ["createdUtc"] = trackedPage.CreatedUtc,
            ["lastTouchedUtc"] = trackedPage.LastTouchedUtc,
            ["consoleMessageCount"] = trackedPage.ConsoleMessages.Count,
            ["networkEventCount"] = trackedPage.NetworkEvents.Count,
            ["pageErrorCount"] = trackedPage.PageErrors.Count,
        };

        if (!trackedPage.Page.IsClosed)
        {
            snapshot["title"] = await trackedPage.Page.TitleAsync();
        }

        return snapshot;
    }

    private static void EnqueueLimited<T>(ConcurrentQueue<T> queue, T item, int limit)
    {
        queue.Enqueue(item);
        while (queue.Count > limit && queue.TryDequeue(out _))
        {
        }
    }

    private static string NormalizeBrowserType(string browserType)
    {
        if (string.IsNullOrWhiteSpace(browserType))
        {
            return "chromium";
        }

        browserType = browserType.Trim().ToLowerInvariant();
        return browserType switch
        {
            "chromium" or "chrome" => "chromium",
            "firefox" => "firefox",
            "webkit" => "webkit",
            _ => throw new InvalidOperationException($"Unsupported browser type '{browserType}'. Supported values are chromium, firefox, and webkit."),
        };
    }
}
