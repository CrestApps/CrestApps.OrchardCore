using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

/// <summary>
/// Waits until a selector appears on the page.
/// </summary>
public sealed class BrowserWaitForTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "required": ["selector"],
          "properties": {
            "selector": {
              "type": "string",
              "description": "CSS selector to wait for."
            },
            "timeout_ms": {
              "type": "integer",
              "description": "Maximum milliseconds to wait. Defaults to 10000."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.WaitFor;
    public override string Description => "Waits until a CSS selector is visible on the current page.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return ExecuteSessionStepAsync(arguments, cancellationToken, async (session, token) =>
        {
            var selector = arguments["selector"]?.ToString();
            if (string.IsNullOrWhiteSpace(selector))
            {
                return "Parameter 'selector' is required.";
            }

            var timeoutMs = 10_000;
            if (arguments.TryGetValue("timeout_ms", out var timeoutValue)
                && timeoutValue is JsonElement jsonTimeout
                && jsonTimeout.ValueKind == JsonValueKind.Number)
            {
                timeoutMs = Math.Clamp(jsonTimeout.GetInt32(), 500, 60_000);
            }

            var observationService = arguments.Services.GetRequiredService<IPlaywrightObservationService>();
            await session.Page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = timeoutMs,
            }).WaitAsync(token);
            var observation = await observationService.CaptureAsync(session, token);

            return Serialize(new
            {
                found = selector,
                observation,
            });
        });
    }
}
