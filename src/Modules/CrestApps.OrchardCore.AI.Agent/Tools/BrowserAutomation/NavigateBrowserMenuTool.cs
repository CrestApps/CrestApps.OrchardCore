using System.Diagnostics;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Services;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Agent.Tools.BrowserAutomation;

public sealed class NavigateBrowserMenuTool : BrowserAutomationToolBase<NavigateBrowserMenuTool>
{
    public const string TheName = "navigateBrowserMenu";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "sessionId": {
              "type": "string",
              "description": "The browser session identifier."
            },
            "pageId": {
              "type": "string",
              "description": "Optional page identifier. Defaults to the active tab."
            },
            "path": {
              "type": "string",
              "description": "A menu label path to follow, such as 'Search >> Indexes' or 'Content Definitions'."
            },
            "pathSegments": {
              "type": "array",
              "description": "Optional explicit menu path segments, such as ['Search', 'Indexes'].",
              "items": {
                "type": "string"
              }
            },
            "timeoutMs": {
              "type": "integer",
              "description": "Optional timeout in milliseconds."
            }
          },
          "required": [],
          "additionalProperties": false
        }
        """);

    public NavigateBrowserMenuTool(BrowserAutomationService browserAutomationService, ILogger<NavigateBrowserMenuTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Navigates visible site or Orchard Core admin menus by label, including nested paths such as 'Search >> Indexes'. Use this when the user asks to open, visit, or go to a page from the UI navigation.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var sessionId = GetSessionId(arguments);
            var pageId = GetPageId(arguments);
            var timeout = GetTimeout(arguments);
            var pathSegments = GetPathSegments(arguments);

            var result = await BrowserAutomationService.WithPageAsync(sessionId, pageId, async (_, trackedPage) =>
            {
                var matchedItems = new List<NavigationMatch>();

                foreach (var pathSegment in pathSegments)
                {
                    var match = await ClickNavigationItemAsync(trackedPage.Page, pathSegment, timeout);
                    matchedItems.Add(match);
                }

                return new
                {
                    sessionId,
                    pageId = trackedPage.PageId,
                    pathSegments,
                    matchedItems,
                    title = await trackedPage.Page.TitleAsync(),
                    url = trackedPage.Page.Url,
                };
            }, cancellationToken);

            RequestLiveNavigation(result.url);
            return Success(TheName, result);
        });
    }

    private static string[] GetPathSegments(AIFunctionArguments arguments)
    {
        if (arguments.TryGetFirst<string[]>("pathSegments", out var pathSegments) && pathSegments is { Length: > 0 })
        {
            var sanitizedSegments = pathSegments
                .Select(BrowserNavigationPathParser.NormalizeSegment)
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .ToArray();

            if (sanitizedSegments.Length > 0)
            {
                return sanitizedSegments;
            }
        }

        var path = GetOptionalString(arguments, "path");
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("path or pathSegments is required.");
        }

        var parsedSegments = BrowserNavigationPathParser.Split(path);
        if (parsedSegments.Length == 0)
        {
            throw new InvalidOperationException("path or pathSegments is required.");
        }

        return parsedSegments;
    }

    private static async Task<NavigationMatch> ClickNavigationItemAsync(IPage page, string pathSegment, int timeout)
    {
        var marker = $"ai-nav-{Guid.NewGuid():N}";
        var rawMatch = await page.EvaluateAsync<string>(BrowserAutomationScripts.FindNavigationItem, new
        {
            segment = pathSegment,
            marker,
        });

        if (string.IsNullOrWhiteSpace(rawMatch))
        {
            throw new InvalidOperationException($"Could not find a navigation item matching '{pathSegment}'.");
        }

        var match = JsonSerializer.Deserialize<NavigationMatch>(rawMatch);
        if (match is null)
        {
            throw new InvalidOperationException($"Could not resolve the navigation item '{pathSegment}'.");
        }

        var previousUrl = page.Url;
        var locator = page.Locator($"[data-ai-nav-match='{marker}']").First;

        await locator.ScrollIntoViewIfNeededAsync(new LocatorScrollIntoViewIfNeededOptions
        {
            Timeout = timeout,
        });

        await locator.ClickAsync(new LocatorClickOptions
        {
            Timeout = timeout,
        });

        await page.EvaluateAsync(BrowserAutomationScripts.RemoveNavigationMarker, marker);

        if (!string.IsNullOrWhiteSpace(match.Href) && !match.Href.StartsWith('#'))
        {
            await WaitForUrlChangeAsync(page, previousUrl, Math.Min(timeout, 5_000));
        }
        else
        {
            await page.WaitForTimeoutAsync(300);
        }

        match.PathSegment = pathSegment;

        return match;
    }

    private static async Task WaitForUrlChangeAsync(IPage page, string previousUrl, int timeoutMs)
    {
        var stopwatch = Stopwatch.StartNew();

        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            if (!string.Equals(page.Url, previousUrl, StringComparison.Ordinal))
            {
                return;
            }

            await page.WaitForTimeoutAsync(100);
        }
    }

    private sealed class NavigationMatch
    {
        public string PathSegment { get; set; }

        public string Text { get; set; }

        public string Href { get; set; }

        public string TagName { get; set; }

        public string AriaExpanded { get; set; }
    }
}
