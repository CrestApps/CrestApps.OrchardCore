using System.Text.Json;
using CrestApps.OrchardCore.Commerce.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Commerce.AI;

/// <summary>
/// Marks a setup step as skipped, or clears the skip, as the Skip and Undo buttons of Commerce › Set up your store do
/// (<see cref="StoreSetupGuide.SkipAsync"/>). It changes only the guide's progress.
/// </summary>
internal sealed class SkipStoreSetupStepTool : CommerceAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "skipStoreSetupStep";

    private static readonly JsonElement _jsonSchema = Schema(
    """
    {
      "type": "object",
      "properties": {
        "step": {
          "type": "string",
          "description": "The step's name, from getStoreSetupGuide."
        },
        "undo": {
          "type": "boolean",
          "description": "true to clear the skip, so the guide asks for the step again. false or left out to skip it."
        }
      },
      "required": ["step"],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Marks a setup step as skipped, or clears the skip, so the guide moves on. It changes only the guide's progress.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var name = ReadString(arguments, "step");
        var step = StoreSetupGuide.Find(services.GetServices<IStoreSetupStep>(), name);

        if (step is null)
        {
            return $"Nothing was done: there is no setup step named '{name}'. Use getStoreSetupGuide for the steps.";
        }

        var undo = ReadBool(arguments, "undo") == true;

        await StoreSetupGuide.SkipAsync(step, undo, services.GetRequiredService<ISiteService>());

        return Json(new
        {
            step = step.Name,
            skipped = !undo,
            message = undo
                ? $"The step '{step.Name}' is no longer skipped; the guide asks for it again."
                : $"The step '{step.Name}' is skipped. A skipped step that is done anyway still shows as done.",
        });
    }
}
