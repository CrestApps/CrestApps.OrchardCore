using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Agent.BrowserAutomation;

public sealed class UncheckBrowserElementTool : BrowserAutomationToolBase<UncheckBrowserElementTool>
{
    public const string TheName = "uncheckBrowserElement";

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
            }
          },
          "required": [
            "sessionId",
            "selector"
          ],
          "additionalProperties": false
        }
        """);

    public UncheckBrowserElementTool(BrowserAutomationService browserAutomationService, ILogger<UncheckBrowserElementTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Unchecks a checkbox.";

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
                await locator.UncheckAsync(new LocatorUncheckOptions
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

