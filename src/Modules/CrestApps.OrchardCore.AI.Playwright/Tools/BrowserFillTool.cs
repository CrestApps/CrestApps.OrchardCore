using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

/// <summary>
/// Clears and types a value into an input or textarea element.
/// </summary>
public sealed class BrowserFillTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "required": ["selector", "value"],
          "properties": {
            "selector": {
              "type": "string",
              "description": "CSS selector of the input or textarea to fill."
            },
            "value": {
              "type": "string",
              "description": "The text value to type into the field."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.Fill;
    public override string Description => "Fills an input using a raw selector and returns the observed page state.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return ExecuteSessionStepAsync(arguments, cancellationToken, async (session, token) =>
        {
            var selector = arguments["selector"]?.ToString();
            var value = arguments["value"]?.ToString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(selector))
            {
                return "Parameter 'selector' is required.";
            }

            var observationService = arguments.Services.GetRequiredService<IPlaywrightObservationService>();
            var actionVisualizer = arguments.Services.GetRequiredService<IPlaywrightActionVisualizer>();
            var locator = session.Page.Locator(selector).First;

            try
            {
                await actionVisualizer.ShowLocatorActionAsync(session.Page, locator, "Typing", selector, token);
            }
            catch
            {
                await actionVisualizer.ShowPageActionAsync(session.Page, "Typing", selector, token);
            }

            await locator.FillAsync(value).WaitAsync(token);
            var observation = await observationService.CaptureAsync(session, token);

            return Serialize(new
            {
                filled = selector,
                observation,
            });
        });
    }
}
