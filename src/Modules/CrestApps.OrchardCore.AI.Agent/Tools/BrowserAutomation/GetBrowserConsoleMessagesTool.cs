using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using System.Linq;
using CrestApps.OrchardCore.AI.Agent.Services;

namespace CrestApps.OrchardCore.AI.Agent.Tools.BrowserAutomation;

public sealed class GetBrowserConsoleMessagesTool : BrowserAutomationToolBase<GetBrowserConsoleMessagesTool>
{
    public const string TheName = "getBrowserConsoleMessages";

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
              "description": "Optional maximum number of messages to return."
            },
            "includePageErrors": {
              "type": "boolean",
              "description": "Optional. When true, includes captured page error text. Defaults to true."
            }
          },
          "required": [],
          "additionalProperties": false
        }
        """);

    public GetBrowserConsoleMessagesTool(BrowserAutomationService browserAutomationService, ILogger<GetBrowserConsoleMessagesTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Returns recent console messages and page errors captured for the current tab.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var sessionId = GetSessionId(arguments);
            var pageId = GetPageId(arguments);
            var maxItems = GetMaxItems(arguments, 50);
            var includePageErrors = GetBoolean(arguments, "includePageErrors", true);

            var result = await BrowserAutomationService.WithPageAsync(sessionId, pageId, async (_, trackedPage) =>
            {
                var consoleMessages = trackedPage.ConsoleMessages.ToArray().TakeLast(maxItems).ToArray();
                var pageErrors = includePageErrors
                    ? trackedPage.PageErrors.ToArray().TakeLast(maxItems).ToArray()
                    : [];

                return new
                {
                    sessionId,
                    pageId = trackedPage.PageId,
                    title = await trackedPage.Page.TitleAsync(),
                    url = trackedPage.Page.Url,
                    consoleMessages,
                    pageErrors,
                };
            }, cancellationToken);

            return Success(TheName, result);
        });
    }
}

