using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using System;
using System.Linq;
using CrestApps.OrchardCore.AI.Agent.Services;

namespace CrestApps.OrchardCore.AI.Agent.Tools.BrowserAutomation;

public sealed class DiagnoseBrowserPageTool : BrowserAutomationToolBase<DiagnoseBrowserPageTool>
{
    public const string TheName = "diagnoseBrowserPage";

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
              "description": "Optional maximum number of console and network entries to return."
            }
          },
          "required": [],
          "additionalProperties": false
        }
        """);

    public DiagnoseBrowserPageTool(BrowserAutomationService browserAutomationService, ILogger<DiagnoseBrowserPageTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Collects a troubleshooting snapshot for the current page, including visible errors, recent console issues, and failing network requests.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var sessionId = GetSessionId(arguments);
            var pageId = GetPageId(arguments);
            var maxItems = GetMaxItems(arguments, 20);

            var result = await BrowserAutomationService.WithPageAsync(sessionId, pageId, async (_, trackedPage) =>
            {
                var raw = await trackedPage.Page.EvaluateAsync<string>(
                    @"() => JSON.stringify({
                        documentTitle: document.title,
                        readyState: document.readyState,
                        visibleErrors: Array.from(document.querySelectorAll('.error, .alert-danger, .validation-summary-errors, .field-validation-error, .input-validation-error, [aria-invalid=""true""]')).slice(0, 20).map((element, index) => ({
                            index,
                            tagName: element.tagName,
                            text: (element.innerText || element.textContent || '').trim(),
                            id: element.id || '',
                            className: element.className || ''
                        })),
                        brokenImages: Array.from(document.images).filter(image => !image.complete || image.naturalWidth === 0).slice(0, 20).map((image, index) => ({
                            index,
                            src: image.currentSrc || image.src || '',
                            alt: image.alt || ''
                        }))
                    })");

                var failingNetwork = trackedPage.NetworkEvents
                    .ToArray()
                    .Where(x => x.TryGetValue("phase", out var phase) && (phase?.ToString() == "requestfailed" || (x.TryGetValue("status", out var status) && int.TryParse(status?.ToString(), out var code) && code >= 400)))
                    .TakeLast(maxItems)
                    .ToArray();

                var consoleIssues = trackedPage.ConsoleMessages
                    .ToArray()
                    .Where(x => x.TryGetValue("type", out var type) && !string.Equals(type?.ToString(), "info", StringComparison.OrdinalIgnoreCase))
                    .TakeLast(maxItems)
                    .ToArray();

                return new
                {
                    sessionId,
                    pageId = trackedPage.PageId,
                    title = await trackedPage.Page.TitleAsync(),
                    url = trackedPage.Page.Url,
                    diagnostics = ParseJson(raw),
                    consoleIssues,
                    failingNetwork,
                    pageErrors = trackedPage.PageErrors.ToArray().TakeLast(maxItems).ToArray(),
                };
            }, cancellationToken);

            return Success(TheName, result);
        });
    }
}

