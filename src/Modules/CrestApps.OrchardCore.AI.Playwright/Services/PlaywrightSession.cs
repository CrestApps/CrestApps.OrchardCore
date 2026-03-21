using CrestApps.OrchardCore.AI.Playwright.Models;
using Microsoft.Playwright;
using OrchardClock = OrchardCore.Modules.IClock;

namespace CrestApps.OrchardCore.AI.Playwright.Services;

/// <summary>
/// Holds the live Playwright objects for one chat session.
/// </summary>
internal sealed class PlaywrightSession : IPlaywrightSession, IAsyncDisposable
{
    private readonly OrchardClock _clock;
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private CancellationTokenSource _cts = new();

    public string SessionId { get; }
    public string OwnerId { get; private set; }
    public PlaywrightSessionStatus Status { get; private set; } = PlaywrightSessionStatus.Idle;
    public string CurrentUrl { get; internal set; }
    public string CurrentPageTitle { get; internal set; }
    public DateTime CreatedAtUtc { get; }
    public DateTime LastActivityUtc { get; private set; }
    public string BaseUrl { get; private set; }
    public string AdminBaseUrl { get; private set; }
    public PlaywrightBrowserMode BrowserMode { get; internal set; }
    public bool IsAuthenticated { get; internal set; }
    public PlaywrightObservation LastObservation { get; internal set; }
    public int SessionInactivityTimeoutInMinutes { get; private set; }
    public IBrowserContext Context { get; }
    public IPage Page { get; private set; }
    public CancellationToken StopToken => _cts.Token;

    internal PlaywrightSessionRequest Request { get; private set; }

    internal PlaywrightSession(
        PlaywrightSessionRequest request,
        OrchardClock clock,
        IPlaywright playwright,
        IBrowser browser,
        IBrowserContext context,
        IPage page)
    {
        _clock = clock;
        SessionId = request.ChatSessionId;
        OwnerId = request.OwnerId;
        BaseUrl = request.BaseUrl;
        AdminBaseUrl = request.AdminBaseUrl;
        BrowserMode = request.BrowserMode;
        SessionInactivityTimeoutInMinutes = request.SessionInactivityTimeoutInMinutes;
        Request = request;
        _playwright = playwright;
        _browser = browser;
        Context = context;
        Page = page;
        CreatedAtUtc = _clock.UtcNow;
        LastActivityUtc = _clock.UtcNow;
    }

    public void MarkRunning()
    {
        LastActivityUtc = _clock.UtcNow;
        Status = PlaywrightSessionStatus.Running;
    }

    public void MarkIdle()
    {
        LastActivityUtc = _clock.UtcNow;
        if (Status == PlaywrightSessionStatus.Running)
        {
            Status = PlaywrightSessionStatus.Idle;
        }
    }

    public void Stop()
    {
        LastActivityUtc = _clock.UtcNow;
        Status = PlaywrightSessionStatus.Stopped;
        var old = Interlocked.Exchange(ref _cts, new CancellationTokenSource());
        old.Cancel();
        old.Dispose();
    }

    internal void UpdateRequest(PlaywrightSessionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Request = request;
        OwnerId = request.OwnerId;
        BaseUrl = request.BaseUrl;
        AdminBaseUrl = request.AdminBaseUrl;
        SessionInactivityTimeoutInMinutes = request.SessionInactivityTimeoutInMinutes;
        LastActivityUtc = _clock.UtcNow;
    }

    internal void ApplyObservation(PlaywrightObservation observation)
    {
        LastActivityUtc = _clock.UtcNow;
        LastObservation = observation;
        CurrentUrl = observation?.CurrentUrl;
        CurrentPageTitle = observation?.PageTitle;
        IsAuthenticated = observation?.IsAuthenticated ?? false;
    }

    internal async Task<IPage> GetOrCreatePageAsync(CancellationToken cancellationToken = default)
    {
        LastActivityUtc = _clock.UtcNow;
        if (Page?.IsClosed == false)
        {
            return Page;
        }

        var openPage = Context.Pages.FirstOrDefault(page => !page.IsClosed);
        if (openPage is not null)
        {
            Page = openPage;
            return openPage;
        }

        Page = await Context.NewPageAsync().WaitAsync(cancellationToken);
        return Page;
    }

    public async ValueTask DisposeAsync()
    {
        Status = PlaywrightSessionStatus.Closed;
        _cts.Cancel();
        _cts.Dispose();

        try { await Page.CloseAsync(); } catch { /* best effort */ }
        try { await Context.CloseAsync(); } catch { /* best effort */ }
        try { await _browser?.CloseAsync(); } catch { /* best effort */ }

        _playwright.Dispose();
    }
}
