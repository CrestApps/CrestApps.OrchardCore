using System.Text.Json;
using System.Diagnostics;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using CrestApps.OrchardCore.AI.Agent.Services;

namespace CrestApps.OrchardCore.AI.Agent.Tools.BrowserAutomation;

public sealed class WaitForBrowserNavigationTool : BrowserAutomationToolBase<WaitForBrowserNavigationTool>
{
    public const string TheName = "waitForBrowserNavigation";

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
            "urlContains": {
              "type": "string",
              "description": "Optional URL fragment that must appear in the current URL before the wait completes."
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

    public WaitForBrowserNavigationTool(BrowserAutomationService browserAutomationService, ILogger<WaitForBrowserNavigationTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Waits for the page URL to change or to contain the requested fragment.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var sessionId = GetSessionId(arguments);
            var pageId = GetPageId(arguments);
            var urlContains = GetOptionalString(arguments, "urlContains");
            var timeout = GetTimeout(arguments);

            var result = await BrowserAutomationService.WithPageAsync(sessionId, pageId, async (_, trackedPage) =>
            {
                var stopwatch = Stopwatch.StartNew();
                var initialUrl = trackedPage.Page.Url;

                while (stopwatch.ElapsedMilliseconds < timeout)
                {
                    var currentUrl = trackedPage.Page.Url;
                    var changed = !string.Equals(initialUrl, currentUrl, StringComparison.Ordinal);
                    var matches = string.IsNullOrWhiteSpace(urlContains) || currentUrl.Contains(urlContains, StringComparison.OrdinalIgnoreCase);

                    if (changed && matches)
                    {
                        return new
                        {
                            sessionId,
                            pageId = trackedPage.PageId,
                            initialUrl,
                            url = currentUrl,
                            title = await trackedPage.Page.TitleAsync(),
                        };
                    }

                    await Task.Delay(250, cancellationToken);
                }

                throw new TimeoutException($"Navigation did not reach the expected URL within {timeout} ms.");
            }, cancellationToken);

            return Success(TheName, result);
        });
    }
}

