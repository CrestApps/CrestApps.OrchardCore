using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.AI.Recipes;

internal sealed class AIDeploymentStep : NamedRecipeStepHandler
{
    /// <summary>
    /// The recipe step key used to identify this handler.
    /// </summary>
    public const string StepKey = "AIDeployment";

    private readonly IAIDeploymentManager _manager;
    private readonly AIOptions _aiOptions;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIDeploymentStep"/> class.
    /// </summary>
    /// <param name="manager">The AI deployment manager.</param>
    /// <param name="aiOptions">The AI configuration options.</param>
    /// <param name="stringLocalizer">The string localizer for error messages.</param>
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
            var isNew = false;

            var id = token[nameof(AIDeployment.ItemId)]?.GetValue<string>();

            var hasId = !string.IsNullOrEmpty(id);

            if (hasId)
            {
                deployment = await _manager.FindByIdAsync(id);
            }

#pragma warning disable CS0618 // Type or member is obsolete
            var sourceName = token[nameof(AIDeployment.ClientName)]?.GetValue<string>()
                ?? token[nameof(AIDeployment.ProviderName)]?.GetValue<string>();
#pragma warning restore CS0618 // Type or member is obsolete
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
                isNew = true;
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

                if (hasId && UniqueId.IsValid(id))
                {
                    deployment.ItemId = id;
                }
            }

            if (TryGetDeploymentPurpose(token[nameof(AIDeployment.Purpose)], out var deploymentPurpose) ||
                TryGetDeploymentPurpose(token["Type"], out deploymentPurpose))
            {
                deployment.Purpose = deploymentPurpose;
            }
            else
            {
                // Default to Chat for backward compatibility with recipes
                // that do not include the purpose property.
                deployment.Purpose = AIDeploymentPurpose.Chat;
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

            if (isNew)
            {
                await _manager.CreateAsync(deployment);
            }
        }
    }

    private sealed class AIModelDeploymentStepModel
    {
        /// <summary>
        /// Gets or sets the collection of AI deployment definitions to import.
        /// </summary>
        public JsonArray Deployments { get; set; }
    }

    private static bool TryGetDeploymentPurpose(JsonNode typeNode, out AIDeploymentPurpose purpose)
    {
        purpose = AIDeploymentPurpose.None;

        if (typeNode is null)
        {
            return false;
        }

        if (typeNode is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is null ||
                    !Enum.TryParse<AIDeploymentPurpose>(item.GetValue<string>(), ignoreCase: true, out var parsedPurpose) ||
                        parsedPurpose == AIDeploymentPurpose.None)
                {
                    purpose = AIDeploymentPurpose.None;
                    return false;
                }

                purpose |= parsedPurpose;
            }

            return purpose.IsValidSelection();
        }

        var typeValue = typeNode.GetValue<string>();

        return !string.IsNullOrEmpty(typeValue) &&
            Enum.TryParse(typeValue, ignoreCase: true, out purpose) &&
                purpose.IsValidSelection();
    }
}
