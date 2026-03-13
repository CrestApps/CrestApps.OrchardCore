using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

public sealed class CaptureStateTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "properties": {},
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.CaptureState;
    public override string Description => "Captures the current browser URL, title, heading, toast message, validation messages, and a short control summary.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        => ExecuteObservationStepAsync(
            arguments,
            cancellationToken,
            "capture_state",
            (session, token) => arguments.Services.GetRequiredService<IOrchardAdminPlaywrightService>().CaptureStateAsync(session, token));
}
