using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using System.Linq;
using CrestApps.OrchardCore.AI.Agent.Services;

namespace CrestApps.OrchardCore.AI.Agent.Tools.BrowserAutomation;

public sealed class GetBrowserNetworkActivityTool : BrowserAutomationToolBase<GetBrowserNetworkActivityTool>
{
    public const string TheName = "getBrowserNetworkActivity";

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
            "maxItems": {
              "type": "integer",
              "description": "Optional maximum number of network events to return."
            }
          },
          "required": [],
          "additionalProperties": false
        }
        """);

    public GetBrowserNetworkActivityTool(BrowserAutomationService browserAutomationService, ILogger<GetBrowserNetworkActivityTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Returns recent network requests, responses, and failed requests captured for the current tab.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var sessionId = GetSessionId(arguments);
            var pageId = GetPageId(arguments);
            var maxItems = GetMaxItems(arguments, 50);

            var result = await BrowserAutomationService.WithPageAsync(sessionId, pageId, async (_, trackedPage) =>
            {
                var events = trackedPage.NetworkEvents.ToArray().TakeLast(maxItems).ToArray();

                return new
                {
                    sessionId,
                    pageId = trackedPage.PageId,
                    title = await trackedPage.Page.TitleAsync(),
                    url = trackedPage.Page.Url,
                    events,
                };
            }, cancellationToken);

            return Success(TheName, result);
        });
    }
}

