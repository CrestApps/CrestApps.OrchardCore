using System.Collections.Concurrent;
using CrestApps.OrchardCore.Telephony.PlaywrightTests.Infrastructure;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.Telephony.PlaywrightTests.Resources;

/// <summary>
/// The <c>data-confirm</c> and <c>data-once</c> form bindings of the shared <c>crestapps-browser</c> script, run as the
/// built bundle in Chromium and driven with real clicks.
/// </summary>
/// <remarks>
/// A scripted <c>button.click()</c> and a fake page both hide how a browser sends a submit a person clicks: promise
/// callbacks run between the listeners, while the browser is still sending the submit, and a submit asked for then is
/// ignored. On the storefront that left the form unsent after the customer said yes, and let the next click through
/// with no question (deleting an address, stopping a renewal, cancelling an appointment).
/// </remarks>
public sealed class ConfirmFormBindingsTests : IAsyncLifetime
{
    private const string Origin = "http://storefront.test";
    private const string ScriptPath = "/scripts/crestapps-browser.min.js";

    private static readonly Lazy<string> _script = new(ReadBuiltScript);

    private TestBrowser _browser;

    /// <inheritdoc/>
    public async ValueTask InitializeAsync()
    {
        var fixture = await TestContext.Current.GetFixture<PlaywrightBrowserFixture>()
            ?? throw new InvalidOperationException($"The {nameof(PlaywrightBrowserFixture)} assembly fixture is not registered.");

        _browser = new TestBrowser(fixture.NextBrowser());
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }
    }

    [Fact]
    public async Task Storefront_AClickTheCustomerConfirms_IsSentOnce_AndDisablesTheButton()
    {
        // Arrange
        var site = await StorefrontAsync(StorefrontForm);
        site.AnswerDialogs(accept: true);

        // Act
        await site.Page.ClickAsync("#remove");

        // Assert - asked once, sent at once, and the button is off so a second click sends nothing.
        await site.WaitForPostsAsync(1);
        Assert.Equal(["Remove this address?"], site.Questions.ToArray());
        Assert.Equal(["/account/addresses/remove"], site.Posts.ToArray());
        Assert.True(await site.Page.Locator("#remove").IsDisabledAsync());
    }

    [Fact]
    public async Task Storefront_AClickTheCustomerDeclines_IsNotSent_AndTheNextClickAsksAgain()
    {
        // Arrange
        var site = await StorefrontAsync(StorefrontForm);
        site.AnswerDialogs(accept: false);

        // Act
        await site.Page.ClickAsync("#remove");
        await site.Page.WaitForTimeoutAsync(300);

        // Assert
        Assert.Equal(["Remove this address?"], site.Questions.ToArray());
        Assert.Empty(site.Posts);
        Assert.False(await site.Page.Locator("#remove").IsDisabledAsync());

        // The next click is asked about too, and goes once the customer agrees.
        site.AnswerDialogs(accept: true);
        await site.Page.ClickAsync("#remove");
        await site.WaitForPostsAsync(1);
        Assert.Equal(["Remove this address?", "Remove this address?"], site.Questions.ToArray());
        Assert.Equal(["/account/addresses/remove"], site.Posts.ToArray());
    }

    [Fact]
    public async Task Storefront_AButtonWithItsOwnQuestion_IsAskedAbout_AndPostsWhereItSends()
    {
        // Arrange
        var site = await StorefrontAsync("""
            <form target="result" method="post" action="/booking/save">
                <button id="save" type="submit">Save</button>
                <button id="cancel" type="submit" formaction="/booking/cancel" data-confirm="Cancel my appointment?">Cancel</button>
            </form>
            """);
        site.AnswerDialogs(accept: true);

        // Act
        await site.Page.ClickAsync("#cancel");

        // Assert
        await site.WaitForPostsAsync(1);
        Assert.Equal(["Cancel my appointment?"], site.Questions.ToArray());
        Assert.Equal(["/booking/cancel"], site.Posts.ToArray());
    }

    // The admin theme's dialog answers later, from a click on its own button, so there the submit is held back and asked
    // for again. The stand-in merges its options over its defaults the way TheAdmin's confirmDialog does.
    [Fact]
    public async Task Admin_TheDialogShowsTheThemesDefaultTexts_AndTheSubmitGoesOnceOkIsClicked()
    {
        // Arrange
        var site = await StorefrontAsync(StorefrontForm, AdminDialog);

        // Act
        await site.Page.ClickAsync("#remove");

        // Assert - the defaults stand where the form gives no text of its own.
        Assert.Equal("Are you sure?", await site.Page.Locator("#dialog-title").TextContentAsync());
        Assert.Equal("Remove this address?", await site.Page.Locator("#dialog-message").TextContentAsync());
        Assert.Equal("Yes", await site.Page.Locator("#dialog-ok").TextContentAsync());
        Assert.Equal("No", await site.Page.Locator("#dialog-cancel").TextContentAsync());
        Assert.Empty(site.Posts);

        await site.Page.ClickAsync("#dialog-ok");
        await site.WaitForPostsAsync(1);
        Assert.Equal(["/account/addresses/remove"], site.Posts.ToArray());
    }

    // An agreed submit the browser refuses (a required field emptied while the dialog was open) is not remembered: the
    // next click asks again rather than going through unasked.
    [Fact]
    public async Task Admin_AnAgreedSubmitTheBrowserRefuses_DoesNotLetTheNextClickThroughUnasked()
    {
        // Arrange
        var site = await StorefrontAsync("""
            <form target="result" method="post" action="/admin/refund" data-confirm="Refund this order?">
                <input id="amount" name="amount" required>
                <button id="refund" type="submit">Refund</button>
            </form>
            """, AdminDialog);
        await site.Page.FillAsync("#amount", "10");

        // Act - the browser checks the field again when the agreed submit is asked for, and refuses it.
        await site.Page.ClickAsync("#refund");
        await site.Page.FillAsync("#amount", string.Empty);
        await site.Page.ClickAsync("#dialog-ok");
        await site.Page.WaitForTimeoutAsync(300);
        Assert.Empty(site.Posts);

        await site.Page.FillAsync("#amount", "10");
        await site.Page.ClickAsync("#refund");

        // Assert - asked again rather than sent.
        await site.Page.WaitForTimeoutAsync(300);
        Assert.True(await site.Page.Locator("#dialog-ok").IsVisibleAsync());
        Assert.Empty(site.Posts);

        await site.Page.ClickAsync("#dialog-ok");
        await site.WaitForPostsAsync(1);
        Assert.Equal(["/admin/refund"], site.Posts.ToArray());
    }

    private const string StorefrontForm = """
        <form target="result" method="post" action="/account/addresses/remove" data-confirm="Remove this address?" data-once>
            <button id="remove" type="submit">Remove</button>
        </form>
        """;

    // A stand-in for TheAdmin's confirmDialog: { ...defaults, ...options }, the texts written into the dialog, and the
    // callback called from a click on OK or Cancel.
    private const string AdminDialog = """
        <div id="dialog" hidden>
            <h5 id="dialog-title"></h5><p id="dialog-message"></p>
            <button id="dialog-ok" type="button"></button><button id="dialog-cancel" type="button"></button>
        </div>
        <script>
            window.confirmDialog = function ({ callback, ...options }) {
                const defaults = { title: 'Are you sure?', message: 'Remove this element?', okText: 'Yes', cancelText: 'No' };
                const { title, message, okText, cancelText } = { ...defaults, ...options };
                const dialog = document.getElementById('dialog');
                document.getElementById('dialog-title').innerHTML = `${title}`;
                document.getElementById('dialog-message').innerHTML = `${message}`;
                document.getElementById('dialog-ok').innerHTML = `${okText}`;
                document.getElementById('dialog-cancel').innerHTML = `${cancelText}`;
                dialog.hidden = false;
                const answer = result => { dialog.hidden = true; callback(result); };
                document.getElementById('dialog-ok').onclick = () => answer(true);
                document.getElementById('dialog-cancel').onclick = () => answer(false);
            };
        </script>
        """;

    private async Task<Storefront> StorefrontAsync(string body, string head = "")
    {
        var page = await _browser.NewPageAsync();
        var site = new Storefront(page);

        var html = $"""
            <!doctype html>
            <html><head><meta charset="utf-8">{head}</head>
            <body>{body}<iframe name="result"></iframe><script src="{ScriptPath}"></script></body></html>
            """;

        await page.RouteAsync($"{Origin}/**", async route =>
        {
            var url = new Uri(route.Request.Url);

            if (route.Request.Method == "POST")
            {
                // The forms post into a frame, so the page stays and its buttons can be looked at.
                site.RecordPost(url.AbsolutePath);
                await route.FulfillAsync(new RouteFulfillOptions { Status = 200, ContentType = "text/html", Body = "<p>Done</p>" });

                return;
            }

            if (url.AbsolutePath == ScriptPath)
            {
                await route.FulfillAsync(new RouteFulfillOptions { Status = 200, ContentType = "text/javascript", Body = _script.Value });

                return;
            }

            await route.FulfillAsync(new RouteFulfillOptions { Status = 200, ContentType = "text/html", Body = html });
        });

        await page.GotoAsync($"{Origin}/page");
        await page.WaitForFunctionAsync("() => !!(window.crestapps && window.crestapps.http)");

        return site;
    }

    // The bundle the module ships, as built into its wwwroot; not the unbuilt source.
    private static string ReadBuiltScript()
    {
        const string relative = "src/Modules/CrestApps.OrchardCore.Resources";

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var module = Path.Combine(directory.FullName, relative);

            if (File.Exists(Path.Combine(module, "Assets.json")))
            {
                return File.ReadAllText(Path.Combine(module, "wwwroot", "scripts", "crestapps-browser.min.js"));
            }
        }

        throw new DirectoryNotFoundException($"Could not find {relative} above {AppContext.BaseDirectory}.");
    }

    private sealed class Storefront
    {
        private readonly Lock _gate = new();
        private bool _accept = true;
        private TaskCompletionSource _postsArrived;
        private int _postsWanted;

        public Storefront(IPage page)
        {
            Page = page;
            page.Dialog += async (_, dialog) =>
            {
                Questions.Enqueue(dialog.Message);

                if (_accept)
                {
                    await dialog.AcceptAsync();
                }
                else
                {
                    await dialog.DismissAsync();
                }
            };
        }

        public IPage Page { get; }

        public ConcurrentQueue<string> Questions { get; } = new();

        public ConcurrentQueue<string> Posts { get; } = new();

        public void AnswerDialogs(bool accept)
            => _accept = accept;

        public void RecordPost(string path)
        {
            Posts.Enqueue(path);

            lock (_gate)
            {
                if (_postsArrived is not null && Posts.Count >= _postsWanted)
                {
                    _postsArrived.TrySetResult();
                }
            }
        }

        public async Task WaitForPostsAsync(int count)
        {
            Task arrived;

            lock (_gate)
            {
                _postsWanted = count;
                _postsArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                if (Posts.Count >= count)
                {
                    _postsArrived.TrySetResult();
                }

                arrived = _postsArrived.Task;
            }

            try
            {
                await arrived.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }
            catch (TimeoutException)
            {
                // Reported below, with what was sent.
            }

            // A second submit would follow the first at once: give it the moment it would take, then count.
            await Page.WaitForTimeoutAsync(200);
            Assert.Equal(count, Posts.Count);
        }
    }
}
