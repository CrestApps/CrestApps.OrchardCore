using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

public sealed class ListContentItemsTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "properties": {
            "maxItems": {
              "type": "integer",
              "description": "Maximum number of visible content items to return. Defaults to 10."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.ListContentItems;
    public override string Description => "Lists the visible Orchard content items from the current content items screen.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var maxItems = 10;
        if (arguments.TryGetValue("maxItems", out var maxItemsValue)
            && maxItemsValue is JsonElement jsonMaxItems
            && jsonMaxItems.ValueKind == JsonValueKind.Number)
        {
            maxItems = jsonMaxItems.GetInt32();
        }

        return ExecuteSessionStepAsync(arguments, cancellationToken, async (session, token) =>
        {
            var result = await arguments.Services.GetRequiredService<IOrchardAdminPlaywrightService>()
                .ListVisibleContentItemsAsync(session, maxItems, token);

            return Serialize(result);
        });
    }
}
