using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

[assembly: AssemblyFixture(typeof(PlaywrightBrowserFixture))]

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;

/// <summary>
/// Installs Chromium once for the whole test run and launches a few browsers the tests share, taking turns. Every test
/// still gets its own harness server and its own browser contexts (see <see cref="SoftPhoneBrowserTest"/>), so nothing a
/// page stores or does reaches another test.
/// </summary>
/// <remarks>
/// Each test used to install and launch its own Chromium. The installs queue on one lock, and with every collection
/// running at once a test's setup reached 25 seconds, while a score of browsers starting together starved the pages of
/// the tests already running. One browser for every page went the other way: with every test running at once, its
/// single browser process fell behind and the Telnyx phones never registered. A few spread the pages out.
/// </remarks>
public sealed class PlaywrightBrowserFixture : IAsyncLifetime
{
    private static readonly int _browserCount = Math.Clamp(Environment.ProcessorCount / 4, 1, 6);

    private readonly List<IBrowser> _browsers = [];
    private IPlaywright _playwright;
    private int _nextBrowser = -1;

    /// <summary>
    /// Returns the browser the next test opens its pages in, taking the browsers in turn.
    /// </summary>
    public IBrowser NextBrowser()
        => _browsers[(int)((uint)Interlocked.Increment(ref _nextBrowser) % (uint)_browsers.Count)];

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Playwright browser installation failed with exit code {exitCode}.");
        }

        _playwright = await Playwright.CreateAsync();

        _browsers.AddRange(await Task.WhenAll(Enumerable.Range(0, _browserCount).Select(_ => LaunchAsync(_playwright))));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (var browser in _browsers)
        {
            await browser.DisposeAsync();
        }

        _playwright?.Dispose();
    }

    private static Task<IBrowser> LaunchAsync(IPlaywright playwright)
    {
        return playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,

            // The soft phone sends a real MediaStreamTrack: it swaps the capture into a long-lived send stream, so a
            // scripted stand-in for getUserMedia no longer gets as far as the media adapter. Chromium's fake capture
            // device gives it a real track without a microphone or a permission prompt.
            Args = ["--use-fake-device-for-media-stream", "--use-fake-ui-for-media-stream"],
        });
    }
}
