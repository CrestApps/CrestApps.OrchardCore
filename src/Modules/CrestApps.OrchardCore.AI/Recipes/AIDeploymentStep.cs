using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Recipes;

public sealed class AIDeploymentStep : NamedRecipeStepHandler
{
    private readonly IAIDeploymentManager _deploymentManager;

    internal readonly IStringLocalizer S;

    public AIDeploymentStep(
        IAIDeploymentManager deploymentManager,
        IStringLocalizer<AIDeploymentStep> stringLocalizer)
         : base("AIDeployment")
    {
        _deploymentManager = deploymentManager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<OpenAIModelDeploymentStepModel>();
        var tokens = model.Deployments.Cast<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            AIDeployment deployment = null;

            var id = token[nameof(AIDeployment.Id)]?.GetValue<string>();

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
                var sourceName = token[nameof(AIDeployment.Source)]?.GetValue<string>();

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

    private sealed class OpenAIModelDeploymentStepModel
    {
        public JsonArray Deployments { get; set; }
    }
}
