using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Agent.BrowserAutomation;

public sealed class WaitForBrowserLoadStateTool : BrowserAutomationToolBase<WaitForBrowserLoadStateTool>
{
    public const string TheName = "waitForBrowserLoadState";

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
            "state": {
              "type": "string",
              "description": "Optional load state: load, domcontentloaded, or networkidle."
            },
            "timeoutMs": {
              "type": "integer",
              "description": "Optional timeout in milliseconds."
            }
          },
          "required": [
            "sessionId"
          ],
          "additionalProperties": false
        }
        """);

    public WaitForBrowserLoadStateTool(BrowserAutomationService browserAutomationService, ILogger<WaitForBrowserLoadStateTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Waits for the page to reach a specific load state.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var sessionId = GetSessionId(arguments);
            var pageId = GetPageId(arguments);
            var state = ParseLoadState(arguments);
            var timeout = GetTimeout(arguments);

            var result = await BrowserAutomationService.WithPageAsync(sessionId, pageId, async (_, trackedPage) =>
            {
                await trackedPage.Page.WaitForLoadStateAsync(state, new PageWaitForLoadStateOptions
                {
                    Timeout = timeout,
                });

                return new
                {
                    sessionId,
                    pageId = trackedPage.PageId,
                    state = state.ToString(),
                    url = trackedPage.Page.Url,
                    title = await trackedPage.Page.TitleAsync(),
                };
            }, cancellationToken);

            return Success(TheName, result);
        });
    }
}

