using System.Text.RegularExpressions;
using CrestApps.OrchardCore.AI.Playwright.Models;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Playwright.Services;

public sealed class PlaywrightObservationService : IPlaywrightObservationService
{
    private static readonly string[] HeadingSelectors =
    [
        "main h1",
        ".page-title h1",
        ".page-title",
        "h1",
    ];

    private static readonly string[] ToastSelectors =
    [
        ".toast.show",
        ".notification",
        ".message",
        ".alert-success",
        ".alert-info",
        ".alert-warning",
        ".alert-danger",
    ];

    private static readonly string[] ValidationSelectors =
    [
        ".validation-summary-errors li",
        ".field-validation-error",
        ".alert-danger li",
        ".alert-danger",
        ".titleerror",
        ".exceptionMessage",
        "h1.exceptionMessage",
        ".exception-message",
    ];

    public async Task<PlaywrightObservation> CaptureAsync(IPlaywrightSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var page = session switch
        {
            PlaywrightSession liveSession => await liveSession.GetOrCreatePageAsync(cancellationToken),
            _ => session.Page,
        };
        var title = await page.TitleAsync().WaitAsync(cancellationToken);
        var heading = await GetFirstVisibleTextAsync(page, HeadingSelectors, cancellationToken);
        var toast = await GetFirstVisibleTextAsync(page, ToastSelectors, cancellationToken);
        var validationMessages = await GetTextsAsync(page, ValidationSelectors, cancellationToken);
        var visibleButtons = await GetTextsAsync(page, ["button", "a.btn", "input[type='submit']"], cancellationToken, 6);
        var isLoginPage = await IsLoginPageAsync(page, cancellationToken);

        var observation = new PlaywrightObservation
        {
            CurrentUrl = page.Url,
            PageTitle = title,
            MainHeading = heading,
            ToastMessage = toast,
            ValidationMessages = validationMessages,
            VisibleButtons = visibleButtons,
            IsLoginPage = isLoginPage,
            IsAuthenticated = !isLoginPage,
        };

        if (session is PlaywrightSession concreteSession)
        {
            concreteSession.ApplyObservation(observation);
        }

        return observation;
    }

    private static async Task<bool> IsLoginPageAsync(IPage page, CancellationToken cancellationToken)
    {
        if (page.Url.Contains("/Login", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var username = page.Locator("input[name='UserName'], input[name='Username'], input[name*='UserName'], input[name='Email'], input[name*='Email'], input[type='email'], #UserName, input[id*='UserName'], input[id*='Email']");
        var password = page.Locator("input[name='Password'], input[name*='Password'], input[type='password'], #Password, input[id*='Password']");

        return (await username.CountAsync().WaitAsync(cancellationToken) > 0)
            && (await password.CountAsync().WaitAsync(cancellationToken) > 0);
    }

    private static async Task<string> GetFirstVisibleTextAsync(IPage page, IEnumerable<string> selectors, CancellationToken cancellationToken)
    {
        foreach (var selector in selectors)
        {
            var locator = page.Locator(selector).First;

            if (await locator.CountAsync().WaitAsync(cancellationToken) == 0)
            {
                continue;
            }

            if (await locator.IsVisibleAsync().WaitAsync(cancellationToken))
            {
                var text = await locator.InnerTextAsync().WaitAsync(cancellationToken);
                text = NormalizeText(text);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static async Task<IReadOnlyList<string>> GetTextsAsync(
        IPage page,
        IEnumerable<string> selectors,
        CancellationToken cancellationToken,
        int limit = 4)
    {
        var values = new List<string>();

        foreach (var selector in selectors)
        {
            var locator = page.Locator(selector);
            var count = await locator.CountAsync().WaitAsync(cancellationToken);

            for (var i = 0; i < count && values.Count < limit; i++)
            {
                var current = locator.Nth(i);
                if (!await current.IsVisibleAsync().WaitAsync(cancellationToken))
                {
                    continue;
                }

                var text = current switch
                {
                    _ when selector.Contains("input[type='submit']", StringComparison.OrdinalIgnoreCase)
                        => NormalizeText(await current.GetAttributeAsync("value").WaitAsync(cancellationToken)),
                    _ => NormalizeText(await current.InnerTextAsync().WaitAsync(cancellationToken)),
                };

                if (!string.IsNullOrWhiteSpace(text) && !values.Contains(text, StringComparer.OrdinalIgnoreCase))
                {
                    values.Add(text);
                }
            }

            if (values.Count >= limit)
            {
                break;
            }
        }

        return values;
    }

    private static string NormalizeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Regex.Replace(value.Trim(), @"\s+", " ");
    }
}
