using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

public sealed class GetVisibleWidgetsTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "properties": {
            "maxItems": {
              "type": "integer",
              "description": "Maximum number of visible widget-like items to return. Defaults to 12."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.GetVisibleWidgets;
    public override string Description => "Lists visible widget-like cards, headings, and editor sections on the current page.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var maxItems = 12;
        if (arguments.TryGetValue("maxItems", out var maxItemsValue)
            && maxItemsValue is JsonElement jsonMaxItems
            && jsonMaxItems.ValueKind == JsonValueKind.Number)
        {
            maxItems = jsonMaxItems.GetInt32();
        }

        return ExecuteSessionStepAsync(arguments, cancellationToken, async (session, token) =>
        {
            var result = await arguments.Services.GetRequiredService<IPlaywrightPageInspectionService>()
                .GetVisibleWidgetsAsync(session, maxItems, token);

            return Serialize(result);
        });
    }
}
