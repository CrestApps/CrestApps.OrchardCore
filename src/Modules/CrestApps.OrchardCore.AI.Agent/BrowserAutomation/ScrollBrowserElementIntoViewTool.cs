using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Agent.BrowserAutomation;

public sealed class ScrollBrowserElementIntoViewTool : BrowserAutomationToolBase<ScrollBrowserElementIntoViewTool>
{
    public const string TheName = "scrollBrowserElementIntoView";

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
            "selector": {
              "type": "string",
              "description": "The Playwright selector for the element to scroll into view."
            },
            "timeoutMs": {
              "type": "integer",
              "description": "Optional timeout in milliseconds."
            }
          },
          "required": [
            "sessionId",
            "selector"
          ],
          "additionalProperties": false
        }
        """);

    public ScrollBrowserElementIntoViewTool(BrowserAutomationService browserAutomationService, ILogger<ScrollBrowserElementIntoViewTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Scrolls an element into view using Playwright locator semantics.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var sessionId = GetSessionId(arguments);
            var pageId = GetPageId(arguments);
            var selector = GetRequiredString(arguments, "selector");
            var timeout = GetTimeout(arguments);

            var result = await BrowserAutomationService.WithPageAsync(sessionId, pageId, async (_, trackedPage) =>
            {
                var locator = trackedPage.Page.Locator(selector).First;
                await locator.ScrollIntoViewIfNeededAsync(new LocatorScrollIntoViewIfNeededOptions
                {
                    Timeout = timeout,
                });

                return new
                {
                    sessionId,
                    pageId = trackedPage.PageId,
                    selector,
                    url = trackedPage.Page.Url,
                    title = await trackedPage.Page.TitleAsync(),
                };
            }, cancellationToken);

            return Success(TheName, result);
        });
    }
}

