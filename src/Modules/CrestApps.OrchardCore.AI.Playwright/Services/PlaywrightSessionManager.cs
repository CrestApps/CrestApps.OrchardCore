using System.Collections.Concurrent;
using CrestApps.OrchardCore.AI.Playwright.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using OrchardClock = OrchardCore.Modules.IClock;

namespace CrestApps.OrchardCore.AI.Playwright.Services;

/// <summary>
/// Singleton manager that owns all active Playwright browser sessions.
/// </summary>
public sealed class PlaywrightSessionManager : IPlaywrightSessionManager
{
    private readonly ConcurrentDictionary<string, PlaywrightSession> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly OrchardClock _clock;
    private readonly IPlaywrightObservationService _observationService;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<PlaywrightSessionManager> _logger;

    public PlaywrightSessionManager(
        OrchardClock clock,
        IPlaywrightObservationService observationService,
        IHostEnvironment hostEnvironment,
        ILogger<PlaywrightSessionManager> logger)
    {
        _clock = clock;
        _observationService = observationService;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IPlaywrightSession> GetOrCreateAsync(
        PlaywrightSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(request.ChatSessionId);
        ArgumentException.ThrowIfNullOrEmpty(request.BaseUrl);
        ArgumentException.ThrowIfNullOrEmpty(request.AdminBaseUrl);

        if (_sessions.TryGetValue(request.ChatSessionId, out var existing) &&
            existing.Status != PlaywrightSessionStatus.Closed)
        {
            existing.UpdateRequest(request);
            return existing;
        }

        _logger.LogInformation(
            "Creating Playwright session '{ChatSessionId}' using the dedicated Playwright browser.",
            request.ChatSessionId);

        var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
        var (browser, context, page, actualMode) = await CreateBrowserArtifactsAsync(playwright, request, cancellationToken);
        var session = new PlaywrightSession(request, _clock, playwright, browser, context, page)
        {
            BrowserMode = actualMode,
        };

        _sessions[request.ChatSessionId] = session;

        page = await session.GetOrCreatePageAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(page.Url) || string.Equals(page.Url, "about:blank", StringComparison.OrdinalIgnoreCase))
        {
            await page.GotoAsync(request.AdminBaseUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 30_000,
            }).WaitAsync(cancellationToken);
        }

        await _observationService.CaptureAsync(session, cancellationToken);
        await TryLoginIfNeededAsync(session, cancellationToken);

        return session;
    }

    /// <inheritdoc/>
    public IPlaywrightSession GetSession(string chatSessionId, string ownerId = null)
    {
        _sessions.TryGetValue(chatSessionId, out var session);

        if (session == null || session.Status == PlaywrightSessionStatus.Closed)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(ownerId) && !string.Equals(session.OwnerId, ownerId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return session;
    }

    public async Task EnsureAdminReadyAsync(IPlaywrightSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (session is not PlaywrightSession concreteSession)
        {
            return;
        }

        var page = await concreteSession.GetOrCreatePageAsync(cancellationToken);

        await page.GotoAsync(concreteSession.AdminBaseUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded,
            Timeout = 30_000,
        }).WaitAsync(cancellationToken);

        await _observationService.CaptureAsync(concreteSession, cancellationToken);
        await TryLoginIfNeededAsync(concreteSession, cancellationToken);
    }

    /// <inheritdoc/>
    public void Stop(string chatSessionId, string ownerId = null)
    {
        if (GetSession(chatSessionId, ownerId) is PlaywrightSession session)
        {
            _logger.LogInformation("Stopping Playwright session '{ChatSessionId}'.", chatSessionId);
            session.Stop();
        }
    }

    /// <inheritdoc/>
    public async Task CloseAsync(string chatSessionId, string ownerId = null)
    {
        if (GetSession(chatSessionId, ownerId) is not PlaywrightSession session)
        {
            return;
        }

        if (_sessions.TryRemove(chatSessionId, out _))
        {
            _logger.LogInformation("Closing Playwright session '{ChatSessionId}'.", chatSessionId);
            await session.DisposeAsync();
        }
    }

    public async Task CloseInactiveSessionsAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = _clock.UtcNow;
        var expiredSessions = _sessions.Values
            .Where(session => session.Status != PlaywrightSessionStatus.Closed)
            .Where(session => session.Status != PlaywrightSessionStatus.Running)
            .Where(session => session.LastActivityUtc < utcNow - TimeSpan.FromMinutes(Math.Max(1, session.SessionInactivityTimeoutInMinutes)))
            .ToList();

        foreach (var session in expiredSessions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogDebug(
                "Closing inactive Playwright session '{ChatSessionId}' after {TimeoutMinutes} minutes of inactivity.",
                session.SessionId,
                session.SessionInactivityTimeoutInMinutes);

            await CloseAsync(session.SessionId, session.OwnerId);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, IPlaywrightSession> GetActiveSessions(string ownerId = null)
    {
        return _sessions
            .Where(kvp => kvp.Value.Status != PlaywrightSessionStatus.Closed)
            .Where(kvp => string.IsNullOrWhiteSpace(ownerId)
                || string.Equals(kvp.Value.OwnerId, ownerId, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kvp => kvp.Key, kvp => (IPlaywrightSession)kvp.Value, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<(IBrowser Browser, IBrowserContext Context, IPage Page, PlaywrightBrowserMode ActualMode)> CreateBrowserArtifactsAsync(
        IPlaywright playwright,
        PlaywrightSessionRequest request,
        CancellationToken cancellationToken)
    {
        var userDataDir = ResolvePersistentProfilePath(request);
        Directory.CreateDirectory(userDataDir);

        var persistentContext = await playwright.Chromium.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchPersistentContextOptions
        {
            Headless = request.Headless,
            SlowMo = request.Headless ? 0 : 200,
            ViewportSize = new ViewportSize { Width = 1280, Height = 900 },
        }).WaitAsync(cancellationToken);
        var persistentPage = await PrepareSingleWorkingPageAsync(persistentContext, cancellationToken);

        return (persistentContext.Browser, persistentContext, persistentPage, PlaywrightBrowserMode.PersistentContext);
    }

    private string ResolvePersistentProfilePath(PlaywrightSessionRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.PersistentProfilePath))
        {
            return request.PersistentProfilePath;
        }

        var safeResourceId = string.IsNullOrWhiteSpace(request.ResourceItemId) ? request.ChatSessionId : request.ResourceItemId;
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            safeResourceId = safeResourceId.Replace(invalid, '_');
        }

        return Path.Combine(_hostEnvironment.ContentRootPath, "App_Data", "Playwright", safeResourceId);
    }

    private async Task TryLoginIfNeededAsync(PlaywrightSession session, CancellationToken cancellationToken)
    {
        var observation = session.LastObservation ?? await _observationService.CaptureAsync(session, cancellationToken);
        if (!observation.IsLoginPage)
        {
            return;
        }

        if (!session.Request.CanAttemptLogin)
        {
            _logger.LogInformation(
                "Playwright session '{ChatSessionId}' reached the login page, but no saved login is configured.",
                session.SessionId);
            return;
        }

        _logger.LogInformation(
            "Playwright session '{ChatSessionId}' reached the login page. Attempting sign-in with the saved Playwright account.",
            session.SessionId);

        var page = await session.GetOrCreatePageAsync(cancellationToken);
        await FillFirstVisibleAsync(page, [
            "label:has-text('User name or email') + input",
            "label:has-text('Username') + input",
            "label:has-text('User name') + input",
            "label:has-text('Email') + input",
            "input[name='UserName']",
            "input[name='Username']",
            "input[name*='UserName']",
            "input[name='Email']",
            "input[name*='Email']",
            "input[type='email']",
            "#UserName"
        ], session.Request.Username, cancellationToken);
        await FillFirstVisibleAsync(page, [
            "label:has-text('Password') + input",
            "input[name='Password']",
            "input[name*='Password']",
            "input[type='password']",
            "#Password"
        ], session.Request.Password, cancellationToken);

        if (!await TryClickFirstVisibleAsync(page, ["Log in", "Login", "Sign in", "Sign In"], cancellationToken))
        {
            var submitLocator = page.Locator("button[type='submit'], input[type='submit']").First;
            await submitLocator.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }).WaitAsync(cancellationToken);
        }

        try
        {
            await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new PageWaitForLoadStateOptions
            {
                Timeout = 15_000,
            }).WaitAsync(cancellationToken);
        }
        catch (TimeoutException)
        {
            // Validation errors can keep the page in place; observation below will surface them.
        }

        await _observationService.CaptureAsync(session, cancellationToken);

        if (!session.IsAuthenticated)
        {
            _logger.LogWarning(
                "Playwright session '{ChatSessionId}' could not sign in using the saved Playwright account.",
                session.SessionId);
        }
    }

    private static async Task FillFirstVisibleAsync(
        IPage page,
        IReadOnlyList<string> selectors,
        string value,
        CancellationToken cancellationToken)
    {
        foreach (var selector in selectors)
        {
            var locator = page.Locator(selector).First;
            if (await locator.CountAsync().WaitAsync(cancellationToken) == 0)
            {
                continue;
            }

            if (!await locator.IsVisibleAsync().WaitAsync(cancellationToken))
            {
                continue;
            }

            await locator.FillAsync(value ?? string.Empty, new LocatorFillOptions
            {
                Timeout = 10_000,
            }).WaitAsync(cancellationToken);

            var appliedValue = await locator.InputValueAsync().WaitAsync(cancellationToken);
            if (string.Equals(appliedValue, value ?? string.Empty, StringComparison.Ordinal))
            {
                return;
            }
        }
    }

    private static async Task<bool> TryClickFirstVisibleAsync(
        IPage page,
        IReadOnlyList<string> candidateNames,
        CancellationToken cancellationToken)
    {
        foreach (var candidateName in candidateNames)
        {
            var button = page.GetByRole(AriaRole.Button, new() { Name = candidateName, Exact = true }).First;
            if (await button.CountAsync().WaitAsync(cancellationToken) > 0
                && await button.IsVisibleAsync().WaitAsync(cancellationToken))
            {
                await button.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }).WaitAsync(cancellationToken);
                return true;
            }
        }

        return false;
    }

    private static async Task<IPage> PrepareSingleWorkingPageAsync(
        IBrowserContext context,
        CancellationToken cancellationToken)
    {
        var openPages = context.Pages
            .Where(page => !page.IsClosed)
            .ToList();

        var workingPage = openPages.FirstOrDefault()
            ?? await context.NewPageAsync().WaitAsync(cancellationToken);

        foreach (var extraPage in openPages.Skip(1))
        {
            try
            {
                await extraPage.CloseAsync().WaitAsync(cancellationToken);
            }
            catch
            {
                // Best effort. The working page is the only one we depend on.
            }
        }

        return workingPage;
    }
}
