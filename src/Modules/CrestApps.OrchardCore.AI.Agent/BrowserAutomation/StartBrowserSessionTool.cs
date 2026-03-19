using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.AI.Agent.BrowserAutomation;

public sealed class StartBrowserSessionTool : BrowserAutomationToolBase<StartBrowserSessionTool>
{
    public const string TheName = "startBrowserSession";

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "browserType": {
              "type": "string",
              "description": "Optional browser engine: chromium, firefox, or webkit."
            },
            "headless": {
              "type": "boolean",
              "description": "Optional. When true, launches the browser in headless mode. Defaults to true."
            },
            "startUrl": {
              "type": "string",
              "description": "Optional. The initial URL to open after the browser session starts."
            },
            "viewportWidth": {
              "type": "integer",
              "description": "Optional viewport width in pixels."
            },
            "viewportHeight": {
              "type": "integer",
              "description": "Optional viewport height in pixels."
            },
            "locale": {
              "type": "string",
              "description": "Optional browser locale, such as en-US."
            },
            "userAgent": {
              "type": "string",
              "description": "Optional custom user agent string."
            },
            "timeoutMs": {
              "type": "integer",
              "description": "Optional browser launch and initial navigation timeout in milliseconds."
            }
          },
          "additionalProperties": false
        }
        """);

    public StartBrowserSessionTool(BrowserAutomationService browserAutomationService, ILogger<StartBrowserSessionTool> logger)
        : base(browserAutomationService, logger)
    {
    }

    public override string Name => TheName;

    public override string Description => "Creates a Playwright browser session and optionally navigates to an initial URL.";

    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return await ExecuteSafeAsync(TheName, async () =>
        {
            var snapshot = await BrowserAutomationService.CreateSessionAsync(
                GetOptionalString(arguments, "browserType"),
                GetBoolean(arguments, "headless", true),
                GetOptionalString(arguments, "startUrl"),
                GetNullableInt(arguments, "viewportWidth"),
                GetNullableInt(arguments, "viewportHeight"),
                GetOptionalString(arguments, "locale"),
                GetOptionalString(arguments, "userAgent"),
                GetTimeout(arguments),
                cancellationToken);

            return Success(TheName, snapshot);
        });
    }
}

