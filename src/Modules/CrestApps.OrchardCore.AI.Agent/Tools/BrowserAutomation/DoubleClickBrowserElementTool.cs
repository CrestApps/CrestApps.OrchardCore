using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Services;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Agent.Tools.BrowserAutomation;

public sealed class DoubleClickBrowserElementTool : BrowserAutomationToolBase<DoubleClickBrowserElementTool>
{
    public const string TheName = "doubleClickBrowserElement";

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
              "description": "The Playwright selector for the element."
            },
            "button": {
              "type": "string",
              "description": "Optional mouse button for click actions: left, middle, or right."
            },
            "timeoutMs": {
              "type": "integer",
              "description": "Optional timeout in milliseconds."
            }
          },
          "required": [
            "selector"
          ],
          "additionalProperties": false
        }
        """);

    public DoubleClickBrowserElementTool(BrowserAutomationService browserAutomationService, ILogger<DoubleClickBrowserElementTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Double-clicks an element using a Playwright selector.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var sessionId = GetSessionId(arguments);
            var pageId = GetPageId(arguments);
            var selector = GetRequiredString(arguments, "selector");

            var result = await BrowserAutomationService.WithPageAsync(sessionId, pageId, async (_, trackedPage) =>
            {
                var locator = trackedPage.Page.Locator(selector).First;
                var timeout = GetTimeout(arguments);
                await locator.DblClickAsync(new LocatorDblClickOptions
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

