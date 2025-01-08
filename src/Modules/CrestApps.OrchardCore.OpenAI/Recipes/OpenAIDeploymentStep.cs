using System.Text.Json.Nodes;
using CrestApps.OrchardCore.OpenAI.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.OpenAI.Recipes;

public sealed class OpenAIDeploymentStep : NamedRecipeStepHandler
{
    private readonly IOpenAIDeploymentManager _deploymentManager;

    internal readonly IStringLocalizer S;

    public OpenAIDeploymentStep(
        IOpenAIDeploymentManager deploymentManager,
        IStringLocalizer<OpenAIDeploymentStep> stringLocalizer)
         : base("OpenAIDeployment")
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
            OpenAIDeployment deployment = null;

            var id = token[nameof(OpenAIDeployment.Id)]?.GetValue<string>();

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
                var sourceName = token[nameof(OpenAIDeployment.Source)]?.GetValue<string>();

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
