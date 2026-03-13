using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

public sealed class ClickByRoleTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "required": ["role", "name"],
          "properties": {
            "role": {
              "type": "string",
              "description": "Supported roles: button, link, textbox, menuitem, tab."
            },
            "name": {
              "type": "string",
              "description": "Accessible name for the target control."
            },
            "exact": {
              "type": "boolean",
              "description": "Whether to require an exact accessible-name match."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.ClickByRole;
    public override string Description => "Clicks a visible element by role and accessible name instead of using a brittle selector.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var role = arguments["role"]?.ToString();
        var name = arguments["name"]?.ToString();
        var exactMatch = arguments.TryGetValue("exact", out var exactValue) && exactValue is bool exact && exact;

        if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(name))
        {
            return new ValueTask<object?>("Parameters 'role' and 'name' are required.");
        }

        return ExecuteObservationStepAsync(
            arguments,
            cancellationToken,
            $"click_by_role:{role}:{name}",
            (session, token) => arguments.Services.GetRequiredService<IOrchardAdminPlaywrightService>().ClickByRoleAsync(session, role, name, exactMatch, token));
    }
}
