using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

public sealed class OpenEditorTabTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "required": ["tabName"],
          "properties": {
            "tabName": {
              "type": "string",
              "description": "Visible Orchard editor tab or top-level editor section name to open."
            },
            "exact": {
              "type": "boolean",
              "description": "Whether to require an exact tab-name match."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.OpenEditorTab;
    public override string Description => "Opens a named Orchard editor tab or top-level editor section on the current content editor.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var tabName = arguments["tabName"]?.ToString();
        var exactMatch = arguments.TryGetValue("exact", out var exactValue) && exactValue is bool exact && exact;

        if (string.IsNullOrWhiteSpace(tabName))
        {
            return new ValueTask<object?>("Parameter 'tabName' is required.");
        }

        return ExecuteSessionStepAsync(arguments, cancellationToken, async (session, token) =>
        {
            var result = await arguments.Services.GetRequiredService<IOrchardAdminPlaywrightService>()
                .OpenEditorTabAsync(session, tabName, exactMatch, token);

            return Serialize(result);
        });
    }
}

