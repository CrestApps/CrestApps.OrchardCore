using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Recipes;

public sealed class AIDeploymentStep : NamedRecipeStepHandler
{
    public const string StepKey = "AIDeployment";

    private readonly IAIDeploymentManager _deploymentManager;

    internal readonly IStringLocalizer S;

    public AIDeploymentStep(
        IAIDeploymentManager deploymentManager,
        IStringLocalizer<AIDeploymentStep> stringLocalizer)
         : base(StepKey)
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
                var providerName = token[nameof(AIDeployment.ProviderName)]?.GetValue<string>();

                if (string.IsNullOrEmpty(providerName))
                {
                    context.Errors.Add(S["Could not find provider name. The deployment will not be imported"]);

                    continue;
                }

                var name = token[nameof(AIDeployment.Name)]?.GetValue<string>();

                if (string.IsNullOrEmpty(name))
                {
                    context.Errors.Add(S["Could not find deployment name. The deployment will not be imported"]);

                    continue;
                }

                deployment = await _deploymentManager.FindAsync(providerName, name);

                if (deployment is null)
                {
                    // If we get this far and deployment is still null, we need to create a new deployment

                    deployment = await _deploymentManager.NewAsync(providerName, token);

                    if (deployment == null)
                    {
                        context.Errors.Add(S["Unable to find a provider with the name '{ProviderName}'.", providerName]);

                        continue;
                    }
                }
                else
                {
                    await _deploymentManager.UpdateAsync(deployment, token);
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
