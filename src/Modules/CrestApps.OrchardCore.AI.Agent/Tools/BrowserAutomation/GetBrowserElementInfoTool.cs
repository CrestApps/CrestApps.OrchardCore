using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Services;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Agent.Tools.BrowserAutomation;

public sealed class GetBrowserElementInfoTool : BrowserAutomationToolBase<GetBrowserElementInfoTool>
{
    public const string TheName = "getBrowserElementInfo";

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
              "description": "The Playwright selector for the element to inspect."
            },
            "maxLength": {
              "type": "integer",
              "description": "Optional maximum length for returned text or HTML."
            },
            "timeoutMs": {
              "type": "integer",
              "description": "Optional timeout in milliseconds."
            }
          },
          "required": [
            "selector"
          ],
          "additionalProperties": false
        }
        """);

    public GetBrowserElementInfoTool(BrowserAutomationService browserAutomationService, ILogger<GetBrowserElementInfoTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Returns details about a specific element, including visibility, text, and selected attributes.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var sessionId = GetSessionId(arguments);
            var pageId = GetPageId(arguments);
            var selector = GetRequiredString(arguments, "selector");
            var maxLength = GetMaxTextLength(arguments);
            var timeout = GetTimeout(arguments);

            var result = await BrowserAutomationService.WithPageAsync(sessionId, pageId, async (_, trackedPage) =>
            {
                var locator = trackedPage.Page.Locator(selector).First;
                await locator.WaitForAsync(new LocatorWaitForOptions
                {
                    Timeout = timeout,
                });

                var boundingBox = await locator.BoundingBoxAsync(new LocatorBoundingBoxOptions
                {
                    Timeout = timeout,
                });

                return new
                {
                    sessionId,
                    pageId = trackedPage.PageId,
                    selector,
                    text = Truncate(await locator.InnerTextAsync(new LocatorInnerTextOptions { Timeout = timeout }), maxLength),
                    html = Truncate(await locator.InnerHTMLAsync(new LocatorInnerHTMLOptions { Timeout = timeout }), maxLength),
                    visible = await locator.IsVisibleAsync(),
                    enabled = await locator.IsEnabledAsync(),
                    editable = await locator.IsEditableAsync(),
                    boundingBox,
                    url = trackedPage.Page.Url,
                    title = await trackedPage.Page.TitleAsync(),
                };
            }, cancellationToken);

            return Success(TheName, result);
        });
    }
}

