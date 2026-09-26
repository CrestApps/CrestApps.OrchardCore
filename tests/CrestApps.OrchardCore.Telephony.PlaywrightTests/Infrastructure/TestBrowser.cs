using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;

/// <summary>
/// One test's view of the shared browser: every page it opens is in a context of its own, and all of them are closed
/// when the test ends, so no page of a finished test keeps its phone, its timers or its media running.
/// </summary>
public sealed class TestBrowser : IAsyncDisposable
{
    private readonly IBrowser _browser;
    private readonly List<IBrowserContext> _contexts = [];

    public TestBrowser(IBrowser browser)
    {
        _browser = browser;
    }

    /// <summary>
    /// Opens a page in a new context, as <see cref="IBrowser.NewPageAsync"/> does.
    /// </summary>
    public async Task<IPage> NewPageAsync(BrowserNewPageOptions options = null)
    {
        var page = await _browser.NewPageAsync(options);

        lock (_contexts)
        {
            _contexts.Add(page.Context);
        }

        return page;
    }

    public async ValueTask DisposeAsync()
    {
        IBrowserContext[] contexts;

        lock (_contexts)
        {
            contexts = [.. _contexts];
            _contexts.Clear();
        }

        foreach (var context in contexts)
        {
            await context.CloseAsync();
        }
    }
}
