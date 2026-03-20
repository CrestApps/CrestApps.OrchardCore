using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Services;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Agent.Tools.BrowserAutomation;

public sealed class GoForwardBrowserTool : BrowserAutomationToolBase<GoForwardBrowserTool>
{
    public const string TheName = "goForwardBrowser";

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
            "waitUntil": {
              "type": "string",
              "description": "Optional navigation wait strategy: load, domcontentloaded, networkidle, or commit."
            },
            "timeoutMs": {
              "type": "integer",
              "description": "Optional navigation timeout in milliseconds."
            }
          },
          "required": [],
          "additionalProperties": false
        }
        """);

    public GoForwardBrowserTool(BrowserAutomationService browserAutomationService, ILogger<GoForwardBrowserTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Navigates the tab forward in browser history.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var sessionId = GetSessionId(arguments);
            var pageId = GetPageId(arguments);
            var waitUntil = ParseWaitUntil(arguments);
            var timeout = GetTimeout(arguments);

            var result = await BrowserAutomationService.WithPageAsync(sessionId, pageId, async (_, trackedPage) =>
            {
                var response = await trackedPage.Page.GoForwardAsync(new PageGoForwardOptions
                {
                    Timeout = timeout,
                    WaitUntil = waitUntil,
                });

                return new
                {
                    sessionId,
                    pageId = trackedPage.PageId,
                    url = trackedPage.Page.Url,
                    title = await trackedPage.Page.TitleAsync(),
                    status = response?.Status,
                    ok = response?.Ok,
                };
            }, cancellationToken);

            return Success(TheName, result);
        });
    }
}

