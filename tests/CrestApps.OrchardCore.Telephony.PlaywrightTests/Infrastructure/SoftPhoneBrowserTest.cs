using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;

/// <summary>
/// Starts a fresh harness server and headless Chromium for each test, and carries the page helpers the soft phone
/// browser tests share.
/// </summary>
public abstract class SoftPhoneBrowserTest : IAsyncLifetime
{
    /// <summary>
    /// Gets the harness server.
    /// </summary>
    protected SoftPhoneTestServer Server { get; private set; } = null!;

    /// <summary>
    /// Gets the browser.
    /// </summary>
    protected IBrowser Browser { get; private set; } = null!;

    private IPlaywright _playwright;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);

        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Playwright browser installation failed with exit code {exitCode}.");
        }

        Server = new SoftPhoneTestServer();
        await Server.StartAsync();

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

        if (Server is not null)
        {
            await Server.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Opens the harness page and waits for the phone's hub connection.
    /// </summary>
    protected async Task<IPage> OpenAsync(string query = "")
    {
        var page = await Browser.NewPageAsync();
        await page.GotoAsync(Server.BaseUrl + query);
        await WaitForConnectedAsync(page);

        return page;
    }

    /// <summary>
    /// The size of the CrestApps Soft Phone for Windows window, which hosts the standalone phone in WebView2. Anything the
    /// phone draws has to fit it.
    /// </summary>
    protected static ViewportSize DesktopAppViewport { get; } = new() { Width = 430, Height = 740 };

    /// <summary>
    /// Opens the harness page at <paramref name="viewport"/> and waits for the phone's hub connection.
    /// </summary>
    protected async Task<IPage> OpenAsync(string query, ViewportSize viewport)
    {
        var page = await Browser.NewPageAsync(new BrowserNewPageOptions { ViewportSize = viewport });
        await page.GotoAsync(Server.BaseUrl + query);
        await WaitForConnectedAsync(page);

        return page;
    }

    /// <summary>
    /// Saves a screenshot of <paramref name="page"/> when <c>SOFTPHONE_SCREENSHOT_DIR</c> names a folder, so a person can
    /// look at what a test drove. Nothing is written otherwise.
    /// </summary>
    protected static async Task CaptureAsync(IPage page, string name)
    {
        var directory = Environment.GetEnvironmentVariable("SOFTPHONE_SCREENSHOT_DIR");

        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        Directory.CreateDirectory(directory);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(directory, name + ".png"), FullPage = true });
    }

    /// <summary>
    /// Waits for the phone's hub connection.
    /// </summary>
    protected static async Task WaitForConnectedAsync(IPage page)
    {
        await page.WaitForFunctionAsync(
            """
            () => {
                const el = document.querySelector('#telephony-soft-phone');
                const api = el && el.__telephonySoftPhone;
                const connection = api && api.getConnection && api.getConnection();
                return connection && connection.state === 'Connected';
            }
            """);
    }

    /// <summary>
    /// Enters a number and places the call from the keypad, then has the provider report it connected.
    /// </summary>
    protected static async Task DialAndConnectAsync(IPage page, string number)
    {
        await page.FillAsync("[data-telephony-number]", number);
        await page.ClickAsync("[data-telephony-dial]");
        await PublishLatestCallStateAsync(page);
        await page.Locator("[data-telephony-hold]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Holds the current call and waits for the phone to show it held.
    /// </summary>
    protected static async Task HoldAsync(IPage page)
    {
        await page.ClickAsync("[data-telephony-hold]");
        await PublishLatestCallStateAsync(page);
        await page.Locator("[data-telephony-resume]").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Has the provider report the state of the call it last changed, as a real provider's event would.
    /// </summary>
    protected static async Task PublishLatestCallStateAsync(IPage page)
    {
        await page.EvaluateAsync(
            """
            async () => {
                const connection = window.telephonySoftPhone.getInstance().getConnection();

                for (let attempt = 0; attempt < 20; attempt++) {
                    const published = await connection.invoke('PublishLatestCallState');

                    if (published) {
                        return;
                    }

                    await new Promise(resolve => setTimeout(resolve, 25));
                }

                throw new Error('The test provider did not create a call.');
            }
            """);
    }

    /// <summary>
    /// Has the provider report an inbound call connected.
    /// </summary>
    protected static async Task PublishCallStateAsync(IPage page, string callId, string from)
    {
        await page.EvaluateAsync(
            """
            ([callId, from]) => window.telephonySoftPhone.getInstance().getConnection().invoke(
                'PublishCallState',
                {
                    callId,
                    from,
                    direction: 1,
                    state: 3,
                    providerName: 'InMemory'
                })
            """,
            new[] { callId, from });
    }

    /// <summary>
    /// Shows a Contact Center offer ringing on the phone.
    /// </summary>
    protected static async Task SetIncomingOfferAsync(IPage page, string callId, string from, string reservationId)
    {
        await page.EvaluateAsync(
            """
            ([callId, from, reservationId]) => window.telephonySoftPhone.getInstance().setIncomingOffer(
                { callId, from, direction: 'Inbound', state: 'Ringing', providerName: 'InMemory' },
                { properties: { acceptUrl: '/accept', reservationId } })
            """,
            new[] { callId, from, reservationId });
    }

    /// <summary>
    /// Gets the id of the call the phone shows.
    /// </summary>
    protected static async Task<string> GetCurrentCallIdAsync(IPage page)
    {
        return await page.EvaluateAsync<string>(
            "() => window.telephonySoftPhone.getInstance().getCurrentCall().callId");
    }

    /// <summary>
    /// Asserts the header shows <paramref name="state"/> with the call's running time after it ("In call · 0:03"): the
    /// phone counts the time from the moment the call is first seen connected.
    /// </summary>
    protected static async Task AssertTimedStatusAsync(IPage page, string state)
    {
        var status = (await page.Locator("[data-telephony-status]").InnerTextAsync()).Trim();

        Assert.Matches(new Regex("^" + Regex.Escape(state) + @" · \d+:\d{2}$"), status);
    }

    /// <summary>
    /// Asserts the header shows <paramref name="state"/> alone, with no running time.
    /// </summary>
    protected static async Task AssertStatusAsync(IPage page, string state)
    {
        Assert.Equal(state, (await page.Locator("[data-telephony-status]").InnerTextAsync()).Trim());
    }
}
