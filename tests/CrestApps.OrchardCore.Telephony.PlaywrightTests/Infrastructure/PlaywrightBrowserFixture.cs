using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

[assembly: AssemblyFixture(typeof(PlaywrightBrowserFixture))]

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;

/// <summary>
/// Installs Chromium and launches it once for the whole test run. Every test still gets its own harness server and its
/// own browser contexts (see <see cref="SoftPhoneBrowserTest"/>), so nothing a page stores or does reaches another test.
/// </summary>
/// <remarks>
/// Each test used to install and launch its own Chromium. The installs queue on one lock, and with every collection
/// running at once a test's setup reached 25 seconds, while a score of browsers starting together starved the pages of
/// the tests already running.
/// </remarks>
public sealed class PlaywrightBrowserFixture : IAsyncLifetime
{
    private IPlaywright _playwright;

    /// <summary>
    /// Gets the browser the tests open their pages in.
    /// </summary>
    public IBrowser Browser { get; private set; }

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Playwright browser installation failed with exit code {exitCode}.");
        }

        _playwright = await Playwright.CreateAsync();
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,

            // The soft phone sends a real MediaStreamTrack: it swaps the capture into a long-lived send stream, so a
            // scripted stand-in for getUserMedia no longer gets as far as the media adapter. Chromium's fake capture
            // device gives it a real track without a microphone or a permission prompt.
            Args = ["--use-fake-device-for-media-stream", "--use-fake-ui-for-media-stream"],
        });
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync();
        }

        _playwright?.Dispose();
    }
}
