using System.Text.Json.Nodes;
using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Deployments;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Core.Recipes;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.AI.Recipes;

/// <summary>
/// Imports AI deployments. A deployment is matched by its item id, then by its name and provider. The provider is read
/// from <c>ClientName</c>, or from the older <c>ProviderName</c>.
/// </summary>
internal sealed class AIDeploymentStep : CatalogRecipeStepBase<AIDeployment>
{
    /// <summary>
    /// The recipe step key used to identify this handler.
    /// </summary>
    public const string StepKey = "AIDeployment";

    private readonly IAIDeploymentManager _manager;
    private readonly AIOptions _aiOptions;
    private readonly IAIEndpointPolicy _endpointPolicy;

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
        IAIEndpointPolicy endpointPolicy,
        IStringLocalizer<AIDeploymentStep> stringLocalizer)
        : base(StepKey, "Deployments")
    {
        _manager = manager;
        _aiOptions = aiOptions.Value;
        _endpointPolicy = endpointPolicy;
        S = stringLocalizer;
    }

    protected override ValueTask<AIDeployment> FindByIdAsync(string id)
        => _manager.FindByIdAsync(id);

    protected override async ValueTask<CatalogRecipeLookup<AIDeployment>> FindExistingAsync(JsonObject token, RecipeExecutionContext context)
    {
        var source = GetSource(token);

        if (string.IsNullOrEmpty(source))
        {
            context.Errors.Add(S["Could not find provider name. The deployment will not be imported."]);

            return CatalogRecipeLookup<AIDeployment>.Skip;
        }

        var name = token[nameof(AIDeployment.Name)]?.GetValue<string>()?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            context.Errors.Add(S["Could not find deployment name. The deployment will not be imported"]);

            return CatalogRecipeLookup<AIDeployment>.Skip;
        }

        return CatalogRecipeLookup<AIDeployment>.Found(await _manager.GetAsync(name, source));
    }

    protected override ValueTask UpdateAsync(AIDeployment entry, JsonObject token)
        => _manager.UpdateAsync(entry, token);

    protected override async ValueTask<AIDeployment> NewAsync(JsonObject token, RecipeExecutionContext context)
    {
        var source = GetSource(token);

        if (!_aiOptions.Deployments.TryGetValue(source, out _))
        {
            context.Errors.Add(S["Unable to find a provider-source that can handle the source '{0}'.", source]);

            return null;
        }

        return await _manager.NewAsync(source, token);
    }

    protected override void Preparing(AIDeployment entry, JsonObject token)
    {
        // A legacy Purpose, Capability, or Type the recipe still carries has already been projected onto
        // model capabilities while the deployment was populated. Only the old default is left to apply
        // here: a recipe that names no purpose and declares no capabilities of its own is a text chat
        // deployment, which validation now requires it to say.
        if (!entry.TryGet<AIDeploymentMetadata>(out var metadata) || metadata.Features is not { Length: > 0 })
        {
            entry.Put(new AIDeploymentMetadata
            {
                Features = [AIDeploymentFeatureNames.TextGeneration],
            });
        }
    }

    protected override async ValueTask<ValidationResultDetails> ValidateAsync(AIDeployment entry)
    {
        var result = await _manager.ValidateAsync(entry);

        // The editor refuses an endpoint on the site's own network; a recipe is held to the same rule.
        if (AIRecipeEndpoints.FindRefused(entry, _endpointPolicy) is { } refused)
        {
            result.Fail(new System.ComponentModel.DataAnnotations.ValidationResult(S["The endpoint '{0}' is on the site's own network. Use a public address, or ask the host to allow a private network for AI connections.", refused]));
        }

        return result;
    }

    protected override ValueTask CreateAsync(AIDeployment entry)
        => _manager.CreateAsync(entry);

    private static string GetSource(JsonObject token)
    {
#pragma warning disable CS0618 // Type or member is obsolete
        return token[nameof(AIDeployment.ClientName)]?.GetValue<string>()
            ?? token[nameof(AIDeployment.ProviderName)]?.GetValue<string>();
#pragma warning restore CS0618 // Type or member is obsolete
    }
}
