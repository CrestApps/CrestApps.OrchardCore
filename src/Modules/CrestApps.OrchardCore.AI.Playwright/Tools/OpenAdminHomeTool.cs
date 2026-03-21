using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

public sealed class OpenAdminHomeTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "properties": {},
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.OpenAdminHome;
    public override string Description => "Navigates to the Orchard admin home for the current tenant and validates whether the browser session is authenticated.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        => ExecuteObservationStepAsync(
            arguments,
            cancellationToken,
            "open_admin_home",
            (session, token) => arguments.Services.GetRequiredService<IOrchardAdminPlaywrightService>().OpenAdminHomeAsync(session, token));
}
