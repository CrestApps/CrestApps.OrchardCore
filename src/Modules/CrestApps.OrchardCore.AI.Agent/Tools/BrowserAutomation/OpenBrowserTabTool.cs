using System.Text.Json;
using CrestApps.OrchardCore.AI.Agent.Services;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Agent.Tools.BrowserAutomation;

public sealed class OpenBrowserTabTool : BrowserAutomationToolBase<OpenBrowserTabTool>
{
    public const string TheName = "openBrowserTab";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "sessionId": {
              "type": "string",
              "description": "The browser session identifier."
            },
            "url": {
              "type": "string",
              "description": "Optional URL to open in the new tab."
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

    public OpenBrowserTabTool(BrowserAutomationService browserAutomationService, ILogger<OpenBrowserTabTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Opens a new tab in an existing browser session and can optionally navigate it to a URL.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var snapshot = await BrowserAutomationService.CreatePageAsync(
                GetSessionId(arguments),
                GetOptionalString(arguments, "url"),
                ParseWaitUntil(arguments),
                GetTimeout(arguments),
                cancellationToken);

            return Success(TheName, snapshot);
        });
    }
}

