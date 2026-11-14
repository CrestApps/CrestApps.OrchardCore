using System.Text.Json.Nodes;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Core.Recipes;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.AI.Recipes;

/// <summary>
/// Imports AI tool instances. An instance is matched by its item id, then by its name.
/// </summary>
internal sealed class AIToolInstanceStep : CatalogRecipeStepBase<AIToolInstance>
{
    /// <summary>
    /// The recipe step key used to identify this handler.
    /// </summary>
    public const string StepKey = "AIToolInstances";

    private readonly ISourceCatalogManager<AIToolInstance> _manager;
    private readonly INamedCatalog<AIToolInstance> _instancesCatalog;
    private readonly AIOptions _aiOptions;
    private readonly IAIEndpointPolicy _endpointPolicy;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolInstanceStep"/> class.
    /// </summary>
    /// <param name="manager">The AI tool instance catalog manager.</param>
    /// <param name="instancesCatalog">The named catalog used to resolve instances by name.</param>
    /// <param name="aiOptions">The AI configuration options.</param>
    /// <param name="endpointPolicy">The policy that keeps an instance's addresses off the site's own network.</param>
    /// <param name="stringLocalizer">The string localizer for error messages.</param>
    public AIToolInstanceStep(
        ISourceCatalogManager<AIToolInstance> manager,
        INamedCatalog<AIToolInstance> instancesCatalog,
        IOptions<AIOptions> aiOptions,
        IAIEndpointPolicy endpointPolicy,
        IStringLocalizer<AIToolInstanceStep> stringLocalizer)
        : base(StepKey, "Instances")
    {
        _manager = manager;
        _instancesCatalog = instancesCatalog;
        _aiOptions = aiOptions.Value;
        _endpointPolicy = endpointPolicy;
        S = stringLocalizer;
    }

    protected override ValueTask<AIToolInstance> FindByIdAsync(string id)
        => _manager.FindByIdAsync(id);

    protected override async ValueTask<CatalogRecipeLookup<AIToolInstance>> FindExistingAsync(JsonObject token, RecipeExecutionContext context)
    {
        var name = token[nameof(AIToolInstance.Name)]?.GetValue<string>()?.Trim();

        return CatalogRecipeLookup<AIToolInstance>.Found(string.IsNullOrEmpty(name) ? null : await _instancesCatalog.FindByNameAsync(name));
    }

    protected override ValueTask UpdateAsync(AIToolInstance entry, JsonObject token)
        => _manager.UpdateAsync(entry, token);

    protected override async ValueTask<AIToolInstance> NewAsync(JsonObject token, RecipeExecutionContext context)
    {
        var source = token[nameof(AIToolInstance.Source)]?.GetValue<string>();

        if (string.IsNullOrEmpty(source))
        {
            context.Errors.Add(S["Could not find tool instance source value. The tool instance will not be imported."]);

            return null;
        }

        if (!_aiOptions.ToolInstanceSources.TryGetValue(source, out _))
        {
            context.Errors.Add(S["Unable to find a tool instance source that can handle the source '{0}'.", source]);

            return null;
        }

        return await _manager.NewAsync(source, token);
    }

    protected override async ValueTask<ValidationResultDetails> ValidateAsync(AIToolInstance entry)
    {
        var result = await _manager.ValidateAsync(entry);

        // The editor refuses a base URL, token endpoint or documentation address on the site's own network; a recipe is
        // held to the same rule.
        if (AIRecipeEndpoints.FindRefused(entry, _endpointPolicy, includeUrls: true) is { } refused)
        {
            result.Fail(new System.ComponentModel.DataAnnotations.ValidationResult(S["The address '{0}' is on the site's own network. Use a public address, or ask the host to allow a private network for AI connections.", refused]));
        }

        return result;
    }

    protected override ValueTask CreateAsync(AIToolInstance entry)
        => _manager.CreateAsync(entry);
}
