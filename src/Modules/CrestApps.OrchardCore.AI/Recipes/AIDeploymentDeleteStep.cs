using System.Text.Json.Nodes;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Recipes;

internal sealed class AIDeploymentDeleteStep : NamedRecipeStepHandler
{
    public const string StepKey = "DeleteAIDeployment";

    private readonly IAIDeploymentManager _manager;

    internal readonly IStringLocalizer S;

    public AIDeploymentDeleteStep(
        IAIDeploymentManager manager,
        IStringLocalizer<AIDeploymentDeleteStep> stringLocalizer)
        : base(StepKey)
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<DeleteAIDeploymentStepModel>() ?? new();

        if (model.IncludeAll)
        {
            var deployments = await _manager.GetAllAsync();

            foreach (var deployment in deployments)
            {
                await _manager.DeleteAsync(deployment);
            }

            return;
        }

        if (model.DeploymentNames is null || model.DeploymentNames.Length == 0)
        {
            context.Errors.Add(S["No deployment names were provided."]);
            return;
        }

        foreach (var deploymentName in model.DeploymentNames)
        {
            var name = deploymentName?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var deployment = await _manager.FindByNameAsync(name);

            if (deployment is null)
            {
                context.Errors.Add(S["Unable to find a deployment with the name '{0}'.", name]);
                continue;
            }

            await _manager.DeleteAsync(deployment);
        }
    }

    private sealed class DeleteAIDeploymentStepModel
    {
        public bool IncludeAll { get; set; }

        public string[] DeploymentNames { get; set; }
    }
}
