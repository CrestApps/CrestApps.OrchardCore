using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.OpenAI.Recipes;

public sealed class ModelDeploymentStep : IRecipeStepHandler
{
    private readonly IModelDeploymentManager _deploymentManager;

    internal readonly IStringLocalizer S;

    public ModelDeploymentStep(
        IModelDeploymentManager deploymentManager,
        IStringLocalizer<ModelDeploymentStep> stringLocalizer)
    {
        _deploymentManager = deploymentManager;
        S = stringLocalizer;
    }

    public async Task ExecuteAsync(RecipeExecutionContext context)
    {
        if (!string.Equals(context.Name, "ModelDeployment", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var model = context.Step.ToObject<OpenAIModelDeploymentStepModel>();
        var tokens = model.Deployments.Cast<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            ModelDeployment deployment = null;

            var id = token[nameof(ModelDeployment.Id)]?.GetValue<string>();

            if (!string.IsNullOrEmpty(id))
            {
                deployment = await _deploymentManager.FindByIdAsync(id);
            }

            if (deployment is not null)
            {
                await _deploymentManager.UpdateAsync(deployment, token);
            }
            else
            {
                var sourceName = token[nameof(ModelDeployment.Source)]?.GetValue<string>();

                if (string.IsNullOrEmpty(sourceName))
                {
                    context.Errors.Add(S["Could not find deployment-source value. The deployment will not be imported"]);

                    continue;
                }

                deployment = await _deploymentManager.NewAsync(sourceName, token);

                if (deployment == null)
                {
                    context.Errors.Add(S["Unable to find a deployment-source that can handle the source '{Source}'.", sourceName]);

                    continue;
                }
            }

            var validationResult = await _deploymentManager.ValidateAsync(deployment);

            if (!validationResult.Succeeded)
            {
                foreach (var error in validationResult.Errors)
                {
                    context.Errors.Add(error.ErrorMessage);
                }

                continue;
            }

            await _deploymentManager.SaveAsync(deployment);
        }
    }
}

public sealed class OpenAIModelDeploymentStepModel
{
    public JsonArray Deployments { get; set; }
}