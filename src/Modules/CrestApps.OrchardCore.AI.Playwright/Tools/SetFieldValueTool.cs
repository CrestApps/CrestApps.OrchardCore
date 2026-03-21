using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

public sealed class SetFieldValueTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "required": ["label", "value"],
          "properties": {
            "label": {
              "type": "string",
              "description": "Visible Orchard field label to target."
            },
            "value": {
              "type": "string",
              "description": "Value to write into the field."
            },
            "fieldType": {
              "type": "string",
              "description": "Typed Orchard field strategy. Supported values: auto, text, textarea, select, checkbox.",
              "enum": ["auto", "text", "textarea", "select", "checkbox"]
            },
            "exact": {
              "type": "boolean",
              "description": "Whether to require an exact label match."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.SetFieldValue;
    public override string Description => "Sets an Orchard field by label using a typed field strategy instead of a generic fill action.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var label = arguments["label"]?.ToString();
        var value = arguments["value"]?.ToString() ?? string.Empty;
        var fieldType = arguments["fieldType"]?.ToString() ?? "auto";
        var exactMatch = arguments.TryGetValue("exact", out var exactValue) && exactValue is bool exact && exact;

        if (string.IsNullOrWhiteSpace(label))
        {
            return new ValueTask<object?>("Parameter 'label' is required.");
        }

        return ExecuteSessionStepAsync(arguments, cancellationToken, async (session, token) =>
        {
            var result = await arguments.Services.GetRequiredService<IOrchardAdminPlaywrightService>()
                .SetFieldValueAsync(session, label, value, fieldType, exactMatch, token);

            return Serialize(result);
        });
    }
}

