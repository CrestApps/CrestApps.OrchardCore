using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Agent.BrowserAutomation;

public sealed class SelectBrowserOptionTool : BrowserAutomationToolBase<SelectBrowserOptionTool>
{
    public const string TheName = "selectBrowserOption";

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
              "description": "The Playwright selector for the select element."
            },
            "values": {
              "type": "array",
              "items": {
                "type": "string"
              },
              "description": "One or more option values to select."
            },
            "timeoutMs": {
              "type": "integer",
              "description": "Optional timeout in milliseconds."
            }
          },
          "required": [
            "sessionId",
            "selector",
            "values"
          ],
          "additionalProperties": false
        }
        """);

    public SelectBrowserOptionTool(BrowserAutomationService browserAutomationService, ILogger<SelectBrowserOptionTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Selects one or more values in a select element.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var sessionId = GetSessionId(arguments);
            var pageId = GetPageId(arguments);
            var selector = GetRequiredString(arguments, "selector");
            var values = GetStringArray(arguments, "values");
            var timeout = GetTimeout(arguments);

            var result = await BrowserAutomationService.WithPageAsync(sessionId, pageId, async (_, trackedPage) =>
            {
                var locator = trackedPage.Page.Locator(selector).First;
                var selected = await locator.SelectOptionAsync(values, new LocatorSelectOptionOptions
                {
                    Timeout = timeout,
                });

                return new
                {
                    sessionId,
                    pageId = trackedPage.PageId,
                    selector,
                    values = selected,
                    url = trackedPage.Page.Url,
                    title = await trackedPage.Page.TitleAsync(),
                };
            }, cancellationToken);

            return Success(TheName, result);
        });
    }
}

