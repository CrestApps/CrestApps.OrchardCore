using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Deployment;
using YesSql;
using YSession = YesSql.ISession;

namespace CrestApps.OrchardCore.AI.Agent.Deployment;

/// <summary>
/// Runs a deployment plan with the deployment manager, as the File Download target does, and returns the recipe it
/// builds. Nothing is written to disk: the recipe is kept in memory and the other files the plan adds (media files,
/// custom files) are only named, so a person downloads the full package from the plan page.
/// </summary>
public sealed class ExportDeploymentPlanTool : AIFunction
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "exportDeploymentPlan";

    /// <summary>
    /// The largest recipe, in UTF-8 bytes, the tool returns in the chat.
    /// </summary>
    public const int MaxRecipeBytes = 100_000;

    private static readonly JsonElement _jsonSchema = JsonSerializer.Deserialize<JsonElement>(
    """
    {
      "type": "object",
      "properties": {
        "id": {
          "type": "integer",
          "minimum": 1,
          "description": "The ID of the deployment plan to run, from listDeploymentPlans."
        }
      },
      "required": [
        "id"
      ],
      "additionalProperties": false
    }
    """);

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Runs a deployment plan and returns the generated recipe JSON, the same content as the File Download target, without the other files of the package. The output can contain any site data the plan selects, so use it only when the user asks for an export.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    /// <inheritdoc/>
    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(arguments.Services);

        var services = arguments.Services;

        // As the File Download target does: exporting needs the Export permission.
        if (!await AIToolCallerAuthorization.IsAuthorizedAsync(services, DeploymentPermissions.Export))
        {
            return AIToolCallerAuthorization.NotAllowedMessage;
        }

        var id = arguments.GetFirstValueOrDefault("id", 0L);

        if (id <= 0)
        {
            return "Nothing was exported: give the ID of the plan, from listDeploymentPlans.";
        }

        var plan = await services.GetRequiredService<YSession>().GetAsync<DeploymentPlan>(id);

        if (plan is null)
        {
            return $"Nothing was exported: there is no deployment plan with the ID {id}. Use listDeploymentPlans to find it.";
        }

        var link = AgentToolLinks.GetAdminPath(services, "OrchardCore.Deployment", "DeploymentPlan", "Display", new() { ["id"] = id });
        var result = await DeploymentPlanRunner.RunAsync(services, plan);
        var recipeBytes = result.RecipeTextLength;

        var otherFiles = result.OtherFiles
            .Select(file => new
            {
                path = file.Path,
                size = file.Length,
            })
            .ToList();

        // A recipe that does not fit in the chat is not cut: a cut recipe cannot be imported.
        if (recipeBytes > MaxRecipeBytes)
        {
            return AgentToolResults.Json(new
            {
                exported = false,
                id,
                name = plan.Name,
                recipeSize = recipeBytes,
                recipeSteps = result.Steps.Count,
                otherFiles,
                link,
                note = $"The recipe of this plan is {recipeBytes} bytes, more than the {MaxRecipeBytes} bytes the chat takes. Download the package with File Download on the plan page, or narrow the plan's steps.",
            });
        }

        return AgentToolResults.Json(new
        {
            exported = true,
            id,
            name = plan.Name,
            recipe = result.Recipe,
            recipeSize = recipeBytes,
            otherFiles = otherFiles.Count > 0 ? otherFiles : null,
            link,
            note = otherFiles.Count > 0
                ? "The recipe is returned without the other files of the package listed in otherFiles; download the full package with File Download on the plan page. The name, description and tags of a Recipe File step are not applied to this recipe."
                : "The name, description and tags of a Recipe File step are not applied to this recipe; File Download on the plan page applies them.",
        });
    }
}
