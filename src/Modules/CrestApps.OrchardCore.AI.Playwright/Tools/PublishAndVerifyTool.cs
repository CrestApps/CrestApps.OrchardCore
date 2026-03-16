using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

public sealed class PublishAndVerifyTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "properties": {
            "expectedStatus": {
              "type": "string",
              "description": "Expected Orchard status after publish. Defaults to Published."
            }
          },
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.PublishAndVerify;
    public override string Description => "Publishes the current Orchard content item and returns Orchard-specific verification signals.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var expectedStatus = arguments["expectedStatus"]?.ToString() ?? "Published";

        return ExecuteSessionStepAsync(arguments, cancellationToken, async (session, token) =>
        {
            var result = await arguments.Services.GetRequiredService<IOrchardAdminPlaywrightService>()
                .PublishAndVerifyAsync(session, expectedStatus, token);

            return Serialize(result);
        });
    }
}
