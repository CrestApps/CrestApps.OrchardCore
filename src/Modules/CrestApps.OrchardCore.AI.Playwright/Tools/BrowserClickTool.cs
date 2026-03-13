using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

/// <summary>
/// Clicks an element in the Playwright browser identified by a CSS selector or visible text.
/// </summary>
public sealed class BrowserClickTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "required": ["selector"],
          "properties": {
            "selector": {
              "type": "string",
              "description": "CSS selector or text selector to click."
            },
            "wait_for_navigation": {
              "type": "boolean",
              "description": "When true, waits for the page to finish navigating after the click. Default: false."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.Click;
    public override string Description => "Clicks an element using a raw selector and returns the observed page state.";
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

            var observationService = arguments.Services.GetRequiredService<IPlaywrightObservationService>();
            var actionVisualizer = arguments.Services.GetRequiredService<IPlaywrightActionVisualizer>();
            var waitForNav = arguments.TryGetValue("wait_for_navigation", out var waitValue)
                && waitValue is bool wait && wait;
            var locator = session.Page.Locator(selector).First;

            try
            {
                await actionVisualizer.ShowLocatorActionAsync(session.Page, locator, "AI", $"clicking {selector}", token);
            }
            catch
            {
                await actionVisualizer.ShowPageActionAsync(session.Page, "AI", $"clicking {selector}", token);
            }

            if (waitForNav)
            {
                await locator.ClickAsync(new LocatorClickOptions { Timeout = 10_000 }).WaitAsync(token);
                await session.Page.WaitForLoadStateAsync(
                    LoadState.DOMContentLoaded,
                    new PageWaitForLoadStateOptions { Timeout = 30_000 }).WaitAsync(token);
            }
            else
            {
                await locator.ClickAsync(new LocatorClickOptions
                {
                    Timeout = 10_000,
                }).WaitAsync(token);
            }

            var observation = await observationService.CaptureAsync(session, token);
            return Serialize(new
            {
                clicked = selector,
                observation,
            });
        });
    }
}
