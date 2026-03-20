using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Services;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Agent.Tools.BrowserAutomation;

public sealed class GetBrowserButtonsTool : BrowserAutomationToolBase<GetBrowserButtonsTool>
{
    public const string TheName = "getBrowserButtons";

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
              "description": "Optional maximum number of items to return."
            }
          },
          "required": [],
          "additionalProperties": false
        }
        """);

    public GetBrowserButtonsTool(BrowserAutomationService browserAutomationService, ILogger<GetBrowserButtonsTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Lists button-like controls found on the current page.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var sessionId = GetSessionId(arguments);
            var pageId = GetPageId(arguments);
            var maxItems = GetMaxItems(arguments);

            var result = await BrowserAutomationService.WithPageAsync(sessionId, pageId, async (_, trackedPage) =>
            {
                var raw = await trackedPage.Page.EvaluateAsync<string>(
                    @"(maxItems) => JSON.stringify(Array.from(document.querySelectorAll('button, input[type=button], input[type=submit], input[type=reset]')).slice(0, maxItems).map((button, index) => ({
                        index,
                        tagName: button.tagName, type: button.getAttribute('type') || '', text: (button.innerText || button.value || button.textContent || '').trim(), disabled: !!button.disabled, id: button.id || '', name: button.getAttribute('name') || ''
                    })))",
                    maxItems);

                return new
                {
                    sessionId,
                    pageId = trackedPage.PageId,
                    title = await trackedPage.Page.TitleAsync(),
                    url = trackedPage.Page.Url,
                    items = ParseJson(raw),
                };
            }, cancellationToken);

            return Success(TheName, result);
        });
    }
}

