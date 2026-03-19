using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Agent.BrowserAutomation;

public sealed class ScrollBrowserPageTool : BrowserAutomationToolBase<ScrollBrowserPageTool>
{
    public const string TheName = "scrollBrowserPage";

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
            "deltaX": {
              "type": "integer",
              "description": "Optional horizontal scroll offset. Defaults to 0."
            },
            "deltaY": {
              "type": "integer",
              "description": "Optional vertical scroll offset. Defaults to 400."
            }
          },
          "required": [
            "sessionId"
          ],
          "additionalProperties": false
        }
        """);

    public ScrollBrowserPageTool(BrowserAutomationService browserAutomationService, ILogger<ScrollBrowserPageTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Scrolls the current page by the provided offsets.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var sessionId = GetSessionId(arguments);
            var pageId = GetPageId(arguments);
            var deltaX = arguments.TryGetFirst<int>("deltaX", out var parsedDeltaX) ? parsedDeltaX : 0;
            var deltaY = arguments.TryGetFirst<int>("deltaY", out var parsedDeltaY) ? parsedDeltaY : 400;

            var result = await BrowserAutomationService.WithPageAsync(sessionId, pageId, async (_, trackedPage) =>
            {
                var raw = await trackedPage.Page.EvaluateAsync<string>(
                    @"(scroll) => {
                        window.scrollBy(scroll.deltaX, scroll.deltaY);
                        return JSON.stringify({ x: window.scrollX, y: window.scrollY });
                    }",
                    new { deltaX, deltaY });

                return new
                {
                    sessionId,
                    pageId = trackedPage.PageId,
                    scrollPosition = ParseJson(raw),
                };
            }, cancellationToken);

            return Success(TheName, result);
        });
    }
}

