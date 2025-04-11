using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Recipes;

internal sealed class AIDeploymentStep : NamedRecipeStepHandler
{
    public const string StepKey = "AIDeployment";

    private readonly IAIDeploymentManager _manager;
    private readonly AIOptions _aiOptions;

    internal readonly IStringLocalizer S;

    public AIDeploymentStep(
        IAIDeploymentManager manager,
        IOptions<AIOptions> aiOptions,
        IStringLocalizer<AIDeploymentStep> stringLocalizer)
         : base(StepKey)
    {
        _manager = manager;
        _aiOptions = aiOptions.Value;
        S = stringLocalizer;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var model = context.Step.ToObject<AIModelDeploymentStepModel>();
        var tokens = model.Deployments.Cast<JsonObject>() ?? [];

        foreach (var token in tokens)
        {
            AIDeployment deployment = null;

            var id = token[nameof(AIDeployment.Id)]?.GetValue<string>();

            if (!string.IsNullOrEmpty(id))
            {
                deployment = await _manager.FindByIdAsync(id);
            }

            var sourceName = token[nameof(AIDeployment.ProviderName)]?.GetValue<string>();
            var hasSource = !string.IsNullOrEmpty(sourceName);

            if (deployment is null)
            {
                if (!hasSource)
                {
                    context.Errors.Add(S["Could not find provider name. The deployment will not be imported."]);

                    continue;
                }

                var name = token[nameof(AIDeployment.Name)]?.GetValue<string>()?.Trim();

                if (string.IsNullOrEmpty(name))
                {
                    context.Errors.Add(S["Could not find deployment name. The deployment will not be imported"]);

                    continue;
                }

                if (!string.IsNullOrEmpty(name))
                {
                    deployment = await _manager.GetAsync(name, sourceName);
                }
            }

            if (deployment is not null)
            {
                await _manager.UpdateAsync(deployment, token);
            }
            else
            {
                if (!hasSource)
                {
                    context.Errors.Add(S["Could not find provider name. The deployment will not be imported."]);

                    continue;
                }

                if (!_aiOptions.Deployments.TryGetValue(sourceName, out _))
                {
                    context.Errors.Add(S["Unable to find a provider-source that can handle the source '{0}'.", sourceName]);

                    return;
                }

                deployment = await _manager.NewAsync(sourceName, token);
            }

            var validationResult = await _manager.ValidateAsync(deployment);

            if (!validationResult.Succeeded)
            {
                foreach (var error in validationResult.Errors)
                {
                    context.Errors.Add(error.ErrorMessage);
                }

                continue;
            }

            await _manager.CreateAsync(deployment);
        }
    }

    private sealed class AIModelDeploymentStepModel
    {
        public JsonArray Deployments { get; set; }
    }
}
