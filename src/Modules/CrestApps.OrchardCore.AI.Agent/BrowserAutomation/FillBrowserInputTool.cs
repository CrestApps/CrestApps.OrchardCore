using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Agent.BrowserAutomation;

public sealed class FillBrowserInputTool : BrowserAutomationToolBase<FillBrowserInputTool>
{
    public const string TheName = "fillBrowserInput";

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
              "description": "The Playwright selector for the target control."
            },
            "timeoutMs": {
              "type": "integer",
              "description": "Optional timeout in milliseconds."
            },
            "value": {
              "type": "string",
              "description": "The value to enter."
            }
          },
          "required": [
            "sessionId",
            "selector",
            "value"
          ],
          "additionalProperties": false
        }
        """);

    public FillBrowserInputTool(BrowserAutomationService browserAutomationService, ILogger<FillBrowserInputTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Fills an input, textarea, or content-editable element.";

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
                var value = GetRequiredString(arguments, "value");
                var timeout = GetTimeout(arguments);
                await locator.FillAsync(value, new LocatorFillOptions
                {
                    Timeout = timeout,
                });
                return new
                {
                    sessionId,
                    pageId = trackedPage.PageId,
                    selector,
                    value,
                    url = trackedPage.Page.Url,
                    title = await trackedPage.Page.TitleAsync(),
                };
            }, cancellationToken);

            return Success(TheName, result);
        });
    }
}

