using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Agent.BrowserAutomation;

public sealed class GetBrowserPageStateTool : BrowserAutomationToolBase<GetBrowserPageStateTool>
{
    public const string TheName = "getBrowserPageState";

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
            }
          },
          "required": [
            "sessionId"
          ],
          "additionalProperties": false
        }
        """);

    public GetBrowserPageStateTool(BrowserAutomationService browserAutomationService, ILogger<GetBrowserPageStateTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Returns high-level state about the current page, including title, ready state, scroll position, and element counts.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var sessionId = GetSessionId(arguments);
            var pageId = GetPageId(arguments);

            var result = await BrowserAutomationService.WithPageAsync(sessionId, pageId, async (_, trackedPage) =>
            {
                var raw = await trackedPage.Page.EvaluateAsync<string>(
                    @"() => JSON.stringify({
                        readyState: document.readyState,
                        location: window.location.href,
                        viewportWidth: window.innerWidth,
                        viewportHeight: window.innerHeight,
                        scrollX: window.scrollX,
                        scrollY: window.scrollY,
                        historyLength: window.history.length,
                        linkCount: document.querySelectorAll('a').length,
                        buttonCount: document.querySelectorAll('button, input[type=button], input[type=submit]').length,
                        formCount: document.forms.length,
                        headingCount: document.querySelectorAll('h1, h2, h3, h4, h5, h6').length
                    })");

                return new
                {
                    sessionId,
                    pageId = trackedPage.PageId,
                    title = await trackedPage.Page.TitleAsync(),
                    url = trackedPage.Page.Url,
                    state = ParseJson(raw),
                };
            }, cancellationToken);

            return Success(TheName, result);
        });
    }
}

