using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

public sealed class SetBodyFieldTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "required": ["label", "value"],
          "properties": {
            "label": {
              "type": "string",
              "description": "Visible Orchard body-like field label to target, such as HtmlBody or Body."
            },
            "value": {
              "type": "string",
              "description": "Value to write into the body field."
            },
            "mode": {
              "type": "string",
              "description": "Whether to append to existing content or replace it.",
              "enum": ["append", "replace"]
            },
            "exact": {
              "type": "boolean",
              "description": "Whether to require an exact label match."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.SetBodyField;
    public override string Description => "Sets a body-like Orchard field such as HtmlBody using append or replace behavior and rich-editor detection.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var label = arguments["label"]?.ToString();
        var value = arguments["value"]?.ToString() ?? string.Empty;
        var mode = arguments["mode"]?.ToString() ?? "append";
        var exactMatch = arguments.TryGetValue("exact", out var exactValue) && exactValue is bool exact && exact;
        var append = !mode.Equals("replace", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(label))
        {
            return new ValueTask<object?>("Parameter 'label' is required.");
        }

        return ExecuteSessionStepAsync(arguments, cancellationToken, async (session, token) =>
        {
            var result = await arguments.Services.GetRequiredService<IOrchardAdminPlaywrightService>()
                .SetBodyFieldAsync(session, label, value, mode, exactMatch, token);

            return Serialize(result);
        });
    }
}

