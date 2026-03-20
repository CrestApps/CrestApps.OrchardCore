using System.Collections.Concurrent;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Agent.Services;

internal sealed class BrowserAutomationPage
{
    public BrowserAutomationPage(string pageId, IPage page, DateTime createdUtc)
    {
        PageId = pageId;
        Page = page;
        CreatedUtc = createdUtc;
        LastTouchedUtc = createdUtc;
    }

    public string PageId { get; }

    public IPage Page { get; }

    public DateTime CreatedUtc { get; }

    public DateTime LastTouchedUtc { get; private set; }

    public ConcurrentQueue<Dictionary<string, object>> ConsoleMessages { get; } = new();

    public ConcurrentQueue<Dictionary<string, object>> NetworkEvents { get; } = new();

    public ConcurrentQueue<string> PageErrors { get; } = new();

    public void Touch(DateTime utc)
        => LastTouchedUtc = utc;
}
