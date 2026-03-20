using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Agent.Tools.BrowserAutomation;

public sealed class CaptureBrowserScreenshotTool : BrowserAutomationToolBase<CaptureBrowserScreenshotTool>
{
    public const string TheName = "captureBrowserScreenshot";

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
            "fullPage": {
              "type": "boolean",
              "description": "Optional. When true, captures the full page. Defaults to true."
            },
            "format": {
              "type": "string",
              "description": "Optional screenshot format: png or jpeg. Defaults to png."
            },
            "returnBase64": {
              "type": "boolean",
              "description": "Optional. When true, includes the screenshot content as base64 in the tool result."
            },
            "path": {
              "type": "string",
              "description": "Optional absolute output path. When omitted, a temp path is used."
            }
          },
          "required": [],
          "additionalProperties": false
        }
        """);

    public CaptureBrowserScreenshotTool(BrowserAutomationService browserAutomationService, ILogger<CaptureBrowserScreenshotTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Captures a screenshot of the current page and optionally returns it as base64.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var sessionId = GetSessionId(arguments);
            var pageId = GetPageId(arguments);
            var fullPage = GetBoolean(arguments, "fullPage", true);
            var returnBase64 = GetBoolean(arguments, "returnBase64");
            var format = (GetOptionalString(arguments, "format") ?? "png").Trim().ToLowerInvariant();
            var outputPath = GetOptionalString(arguments, "path");
            var extension = format == "jpeg" || format == "jpg" ? "jpg" : "png";

            var result = await BrowserAutomationService.WithPageAsync(sessionId, pageId, async (_, trackedPage) =>
            {
                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    var directory = Path.Combine(Path.GetTempPath(), "CrestApps.OrchardCore", "BrowserAutomation");
                    Directory.CreateDirectory(directory);
                    outputPath = Path.Combine(directory, $"{sessionId}_{trackedPage.PageId}_{Guid.NewGuid():N}.{extension}");
                }

                var bytes = await trackedPage.Page.ScreenshotAsync(new PageScreenshotOptions
                {
                    FullPage = fullPage,
                    Path = outputPath,
                    Type = extension == "jpg" ? ScreenshotType.Jpeg : ScreenshotType.Png,
                });

                return new
                {
                    sessionId,
                    pageId = trackedPage.PageId,
                    path = outputPath,
                    format = extension,
                    fullPage,
                    base64 = returnBase64 ? Convert.ToBase64String(bytes) : null,
                    url = trackedPage.Page.Url,
                    title = await trackedPage.Page.TitleAsync(),
                };
            }, cancellationToken);

            return Success(TheName, result);
        });
    }
}

