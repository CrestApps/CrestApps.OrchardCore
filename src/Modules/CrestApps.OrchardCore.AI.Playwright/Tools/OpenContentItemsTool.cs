using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

public sealed class OpenContentItemsTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "properties": {},
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.OpenContentItems;
    public override string Description => "Opens the Orchard content items list using the current tenant's admin shell.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        => ExecuteObservationStepAsync(
            arguments,
            cancellationToken,
            "open_content_items",
            (session, token) => arguments.Services.GetRequiredService<IOrchardAdminPlaywrightService>().OpenContentItemsAsync(session, token));
}
