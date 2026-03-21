using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

public sealed class DiagnoseOrchardActionTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "required": ["actionLabel"],
          "properties": {
            "actionLabel": {
              "type": "string",
              "description": "The OrchardCore admin action to locate. Examples: 'Edit', 'Publish Now', 'Save Draft', 'Preview', 'Delete', 'Clone', or any widget action label visible in the admin UI."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.DiagnoseOrchardAction;

    public override string Description =>
        "Attempts to find a named OrchardCore admin action (Edit, Publish Now, Save Draft, Preview, widget actions) " +
        "using priority-ordered locator strategies. When the action is not found, captures a full-page screenshot, " +
        "a container screenshot, and the raw page HTML so the missing action can be diagnosed.";

    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var actionLabel = arguments["actionLabel"]?.ToString();
        if (string.IsNullOrWhiteSpace(actionLabel))
        {
            return new ValueTask<object?>("Parameter 'actionLabel' is required.");
        }

        return ExecuteSessionStepAsync(arguments, cancellationToken, async (session, token) =>
        {
            var result = await arguments.Services
                .GetRequiredService<IOrchardEvidenceService>()
                .FindOrchardElementWithEvidenceAsync(session, actionLabel, token);

            return Serialize(result);
        });
    }
}
