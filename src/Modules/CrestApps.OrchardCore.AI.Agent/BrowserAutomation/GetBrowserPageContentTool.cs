using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Agent.BrowserAutomation;

public sealed class GetBrowserPageContentTool : BrowserAutomationToolBase<GetBrowserPageContentTool>
{
    public const string TheName = "getBrowserPageContent";

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
              "description": "Optional Playwright selector. When omitted, returns the full page content."
            },
            "includeText": {
              "type": "boolean",
              "description": "Optional. Include text content. Defaults to true."
            },
            "includeHtml": {
              "type": "boolean",
              "description": "Optional. Include HTML content. Defaults to false."
            },
            "maxLength": {
              "type": "integer",
              "description": "Optional maximum length for returned text or HTML."
            },
            "timeoutMs": {
              "type": "integer",
              "description": "Optional timeout in milliseconds for selector lookup."
            }
          },
          "required": [
            "sessionId"
          ],
          "additionalProperties": false
        }
        """);

    public GetBrowserPageContentTool(BrowserAutomationService browserAutomationService, ILogger<GetBrowserPageContentTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Retrieves text and/or HTML from the full page or from a specific element.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var sessionId = GetSessionId(arguments);
            var pageId = GetPageId(arguments);
            var selector = GetOptionalString(arguments, "selector");
            var includeText = GetBoolean(arguments, "includeText", true);
            var includeHtml = GetBoolean(arguments, "includeHtml", false);
            var maxLength = GetMaxTextLength(arguments);
            var timeout = GetTimeout(arguments);

            var result = await BrowserAutomationService.WithPageAsync(sessionId, pageId, async (_, trackedPage) =>
            {
                string text = null;
                string html = null;

                if (string.IsNullOrWhiteSpace(selector))
                {
                    if (includeText)
                    {
                        text = await trackedPage.Page.EvaluateAsync<string>("() => document.body ? document.body.innerText : ''");
                    }

                    if (includeHtml)
                    {
                        html = await trackedPage.Page.ContentAsync();
                    }
                }
                else
                {
                    var locator = trackedPage.Page.Locator(selector).First;
                    await locator.WaitForAsync(new LocatorWaitForOptions
                    {
                        Timeout = timeout,
                    });

                    if (includeText)
                    {
                        text = await locator.InnerTextAsync(new LocatorInnerTextOptions
                        {
                            Timeout = timeout,
                        });
                    }

                    if (includeHtml)
                    {
                        html = await locator.InnerHTMLAsync(new LocatorInnerHTMLOptions
                        {
                            Timeout = timeout,
                        });
                    }
                }

                return new
                {
                    sessionId,
                    pageId = trackedPage.PageId,
                    selector,
                    text = includeText ? Truncate(text, maxLength) : null,
                    html = includeHtml ? Truncate(html, maxLength) : null,
                    title = await trackedPage.Page.TitleAsync(),
                    url = trackedPage.Page.Url,
                };
            }, cancellationToken);

            return Success(TheName, result);
        });
    }
}

