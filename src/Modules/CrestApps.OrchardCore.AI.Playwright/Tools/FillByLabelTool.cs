using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

public sealed class FillByLabelTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "required": ["label", "value"],
          "properties": {
            "label": {
              "type": "string",
              "description": "Field label to target."
            },
            "value": {
              "type": "string",
              "description": "Value to fill."
            },
            "exact": {
              "type": "boolean",
              "description": "Whether to require an exact label match."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.FillByLabel;
    public override string Description => "Fills a field by label text instead of using a raw selector. This edits the field only and does not save or publish.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var label = arguments["label"]?.ToString();
        var value = arguments["value"]?.ToString() ?? string.Empty;
        var exactMatch = arguments.TryGetValue("exact", out var exactValue) && exactValue is bool exact && exact;

        if (string.IsNullOrWhiteSpace(label))
        {
            return new ValueTask<object?>("Parameter 'label' is required.");
        }

        return ExecuteObservationStepAsync(
            arguments,
            cancellationToken,
            $"fill_by_label:{label}",
            (session, token) => arguments.Services.GetRequiredService<IOrchardAdminPlaywrightService>().FillByLabelAsync(session, label, value, exactMatch, token));
    }
}
