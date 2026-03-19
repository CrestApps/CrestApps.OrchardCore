using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Agent.BrowserAutomation;

public sealed class UploadBrowserFilesTool : BrowserAutomationToolBase<UploadBrowserFilesTool>
{
    public const string TheName = "uploadBrowserFiles";

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
              "description": "The Playwright selector for the file input."
            },
            "filePaths": {
              "type": "array",
              "items": {
                "type": "string"
              },
              "description": "Absolute file paths to upload."
            },
            "timeoutMs": {
              "type": "integer",
              "description": "Optional timeout in milliseconds."
            }
          },
          "required": [
            "sessionId",
            "selector",
            "filePaths"
          ],
          "additionalProperties": false
        }
        """);

    public UploadBrowserFilesTool(BrowserAutomationService browserAutomationService, ILogger<UploadBrowserFilesTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Uploads one or more files into a file input element.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var sessionId = GetSessionId(arguments);
            var pageId = GetPageId(arguments);
            var selector = GetRequiredString(arguments, "selector");
            var filePaths = GetStringArray(arguments, "filePaths");
            var timeout = GetTimeout(arguments);

            var result = await BrowserAutomationService.WithPageAsync(sessionId, pageId, async (_, trackedPage) =>
            {
                var locator = trackedPage.Page.Locator(selector).First;
                await locator.SetInputFilesAsync(filePaths, new LocatorSetInputFilesOptions
                {
                    Timeout = timeout,
                });

                return new
                {
                    sessionId,
                    pageId = trackedPage.PageId,
                    selector,
                    filePaths,
                    url = trackedPage.Page.Url,
                    title = await trackedPage.Page.TitleAsync(),
                };
            }, cancellationToken);

            return Success(TheName, result);
        });
    }
}

