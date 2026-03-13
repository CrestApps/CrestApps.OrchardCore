using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

public sealed class PublishContentTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "properties": {},
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.PublishContent;
    public override string Description => "Publishes the current Orchard content item and captures the resulting page state.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        => ExecuteObservationStepAsync(
            arguments,
            cancellationToken,
            "publish_content",
            (session, token) => arguments.Services.GetRequiredService<IOrchardAdminPlaywrightService>().PublishContentAsync(session, token));
}
