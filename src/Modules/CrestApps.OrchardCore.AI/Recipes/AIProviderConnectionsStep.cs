using System.Text.Json.Nodes;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Core.Recipes;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.AI.Recipes;

/// <summary>
/// Imports AI provider connections. A connection is matched by its item id, then by its name and source.
/// </summary>
internal sealed class AIProviderConnectionsStep : CatalogRecipeStepBase<AIProviderConnection>
{
    /// <summary>
    /// The recipe step key used to identify this handler.
    /// </summary>
    public const string StepKey = "AIProviderConnections";

    private readonly INamedSourceCatalogManager<AIProviderConnection> _manager;
    private readonly AIOptions _aiOptions;
    private readonly IAIEndpointPolicy _endpointPolicy;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProviderConnectionsStep"/> class.
    /// </summary>
    /// <param name="manager">The AI provider connection manager.</param>
    /// <param name="aiOptions">The AI configuration options.</param>
    /// <param name="stringLocalizer">The string localizer for error messages.</param>
    public AIProviderConnectionsStep(
        INamedSourceCatalogManager<AIProviderConnection> manager,
        IOptions<AIOptions> aiOptions,
        IAIEndpointPolicy endpointPolicy,
        IStringLocalizer<AIProfileStep> stringLocalizer)
        : base(StepKey, "Connections")
    {
        _manager = manager;
        _aiOptions = aiOptions.Value;
        _endpointPolicy = endpointPolicy;
        S = stringLocalizer;
    }

    protected override ValueTask<AIProviderConnection> FindByIdAsync(string id)
        => _manager.FindByIdAsync(id);

    protected override async ValueTask<CatalogRecipeLookup<AIProviderConnection>> FindExistingAsync(JsonObject token, RecipeExecutionContext context)
    {
        var source = GetSource(token);

        if (string.IsNullOrEmpty(source))
        {
            context.Errors.Add(S["Could not find connection-source value. The profile will not be imported."]);

            return CatalogRecipeLookup<AIProviderConnection>.Skip;
        }

        var name = token[nameof(AIProviderConnection.Name)]?.GetValue<string>()?.Trim();

        return CatalogRecipeLookup<AIProviderConnection>.Found(string.IsNullOrEmpty(name) ? null : await _manager.GetAsync(name, source));
    }

    protected override ValueTask UpdateAsync(AIProviderConnection entry, JsonObject token)
        => _manager.UpdateAsync(entry, token);

    protected override async ValueTask<AIProviderConnection> NewAsync(JsonObject token, RecipeExecutionContext context)
    {
        var source = GetSource(token);

        if (!_aiOptions.ConnectionSources.TryGetValue(source, out _))
        {
            context.Errors.Add(S["Unable to find a connection-source that can handle the source '{0}'.", source]);

            return null;
        }

        return await _manager.NewAsync(source, token);
    }

    protected override async ValueTask<ValidationResultDetails> ValidateAsync(AIProviderConnection entry)
    {
        var result = await _manager.ValidateAsync(entry);

        // The editor refuses an endpoint on the site's own network; a recipe is held to the same rule.
        if (AIRecipeEndpoints.FindRefused(entry, _endpointPolicy) is { } refused)
        {
            result.Fail(new System.ComponentModel.DataAnnotations.ValidationResult(S["The endpoint '{0}' is on the site's own network. Use a public address, or ask the host to allow a private network for AI connections.", refused]));
        }

        return result;
    }

    protected override ValueTask CreateAsync(AIProviderConnection entry)
        => _manager.CreateAsync(entry);

    private static string GetSource(JsonObject token)
        => token[nameof(AIProviderConnection.Source)]?.GetValue<string>();
}
