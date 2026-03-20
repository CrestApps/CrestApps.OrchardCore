using System.Collections.Concurrent;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Agent.Services;

internal sealed class BrowserAutomationSession
{
    public BrowserAutomationSession(
        string sessionId,
        string browserType,
        bool headless,
        IPlaywright playwright,
        IBrowser browser,
        IBrowserContext context,
        DateTime createdUtc)
    {
        SessionId = sessionId;
        BrowserType = browserType;
        Headless = headless;
        Playwright = playwright;
        Browser = browser;
        Context = context;
        CreatedUtc = createdUtc;
        LastTouchedUtc = createdUtc;
    }

    public string SessionId { get; }

    public string BrowserType { get; }

    public bool Headless { get; }

    public IPlaywright Playwright { get; }

    public IBrowser Browser { get; }

    public IBrowserContext Context { get; }

    public ConcurrentDictionary<string, BrowserAutomationPage> Pages { get; } = new(StringComparer.OrdinalIgnoreCase);

    public SemaphoreSlim Gate { get; } = new(1, 1);

    public string ActivePageId { get; set; }

    public DateTime CreatedUtc { get; }

    public DateTime LastTouchedUtc { get; private set; }

    public int PageSequence;

    public void Touch(DateTime utc)
        => LastTouchedUtc = utc;
}
