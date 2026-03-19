using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Agent.BrowserAutomation;

public sealed class PressBrowserKeyTool : BrowserAutomationToolBase<PressBrowserKeyTool>
{
    public const string TheName = "pressBrowserKey";

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
            "key": {
              "type": "string",
              "description": "The key or shortcut to press, such as Enter, Escape, Tab, or Control+A."
            },
            "selector": {
              "type": "string",
              "description": "Optional selector to focus before pressing the key."
            },
            "timeoutMs": {
              "type": "integer",
              "description": "Optional timeout in milliseconds."
            }
          },
          "required": [
            "sessionId",
            "key"
          ],
          "additionalProperties": false
        }
        """);

    public PressBrowserKeyTool(BrowserAutomationService browserAutomationService, ILogger<PressBrowserKeyTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Sends a keyboard shortcut or key press to the page or to a focused element.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var sessionId = GetSessionId(arguments);
            var pageId = GetPageId(arguments);
            var key = GetRequiredString(arguments, "key");
            var selector = GetOptionalString(arguments, "selector");
            var timeout = GetTimeout(arguments);

            var result = await BrowserAutomationService.WithPageAsync(sessionId, pageId, async (_, trackedPage) =>
            {
                if (!string.IsNullOrWhiteSpace(selector))
                {
                    var locator = trackedPage.Page.Locator(selector).First;
                    await locator.PressAsync(key, new LocatorPressOptions
                    {
                        Timeout = timeout,
                    });
                }
                else
                {
                    await trackedPage.Page.Keyboard.PressAsync(key, new KeyboardPressOptions());
                }

                return new
                {
                    sessionId,
                    pageId = trackedPage.PageId,
                    selector,
                    key,
                    url = trackedPage.Page.Url,
                    title = await trackedPage.Page.TitleAsync(),
                };
            }, cancellationToken);

            return Success(TheName, result);
        });
    }
}

