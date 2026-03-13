using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

/// <summary>
/// Selects an option in a &lt;select&gt; element by value or visible text.
/// </summary>
public sealed class BrowserSelectTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "required": ["selector", "value"],
          "properties": {
            "selector": {
              "type": "string",
              "description": "CSS selector of the <select> element."
            },
            "value": {
              "type": "string",
              "description": "The option value or visible text label to select."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.Select;
    public override string Description => "Selects an option in a dropdown element using a raw selector.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return ExecuteSessionStepAsync(arguments, cancellationToken, async (session, token) =>
        {
            var selector = arguments["selector"]?.ToString();
            var value = arguments["value"]?.ToString();

            if (string.IsNullOrWhiteSpace(selector) || string.IsNullOrWhiteSpace(value))
            {
                return "Parameters 'selector' and 'value' are both required.";
            }

            var observationService = arguments.Services.GetRequiredService<IPlaywrightObservationService>();
            var selected = await session.Page.SelectOptionAsync(
                selector,
                new SelectOptionValue { Value = value },
                new PageSelectOptionOptions { Timeout = 10_000 }).WaitAsync(token);

            if (selected.Count == 0)
            {
                selected = await session.Page.SelectOptionAsync(
                    selector,
                    new SelectOptionValue { Label = value },
                    new PageSelectOptionOptions { Timeout = 10_000 }).WaitAsync(token);
            }

            var observation = await observationService.CaptureAsync(session, token);
            return Serialize(new
            {
                selected = selector,
                value = selected.FirstOrDefault(),
                observation,
            });
        });
    }
}
