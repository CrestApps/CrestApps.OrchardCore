using System.Text.Json;
using CrestApps.OrchardCore.AI.Playwright.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Playwright.Tools;

public sealed class SaveDraftTool : PlaywrightToolBase
{
    private static readonly JsonElement _schema = JsonSerializer.Deserialize<JsonElement>("""
        {
          "type": "object",
          "properties": {},
          "additionalProperties": false
        }
        """);

    public override string Name => PlaywrightConstants.ToolNames.SaveDraft;
    public override string Description => "Saves the current Orchard content editor as a draft and captures the resulting page state.";
    public override JsonElement JsonSchema => _schema;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        => ExecuteObservationStepAsync(
            arguments,
            cancellationToken,
            "save_draft",
            (session, token) => arguments.Services.GetRequiredService<IOrchardAdminPlaywrightService>().SaveDraftAsync(session, token));
}
