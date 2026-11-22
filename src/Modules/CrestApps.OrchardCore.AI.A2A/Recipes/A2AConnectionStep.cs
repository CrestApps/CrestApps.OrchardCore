using System.Text.Json.Nodes;
using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Recipes;
using CrestApps.OrchardCore.Core.Recipes;
using Microsoft.Extensions.Localization;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.AI.A2A.Recipes;

/// <summary>
/// Imports A2A connections, matched by their item id.
/// </summary>
internal sealed class A2AConnectionStep : CatalogRecipeStepBase<A2AConnection>
{
    public const string StepKey = "A2AConnection";

    private readonly ICatalogManager<A2AConnection> _manager;
    private readonly IAIEndpointPolicy _endpointPolicy;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2AConnectionStep"/> class.
    /// </summary>
    /// <param name="manager">The connection manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    /// <param name="endpointPolicy">The policy the editor holds the agent's endpoint and token endpoint to.</param>
    public A2AConnectionStep(
        ICatalogManager<A2AConnection> manager,
        IStringLocalizer<A2AConnectionStep> stringLocalizer,
        IAIEndpointPolicy endpointPolicy)
        : base(StepKey, "Connections")
    {
        _manager = manager;
        _endpointPolicy = endpointPolicy;
        S = stringLocalizer;
    }

    protected override ValueTask<A2AConnection> FindByIdAsync(string id)
        => _manager.FindByIdAsync(id);

    protected override ValueTask UpdateAsync(A2AConnection entry, JsonObject token)
    {
        // A recipe that leaves a secret out keeps the stored one; when it also changes where the entry sends it, the
        // kept secret is dropped, as the editor drops it.
        return RecipeSecretDestinations.UpdateAsync(entry, token, data => _manager.UpdateAsync(entry, data));
    }

    protected override ValueTask<A2AConnection> NewAsync(JsonObject token, RecipeExecutionContext context)
        => _manager.NewAsync(token);

    protected override async ValueTask<ValidationResultDetails> ValidateAsync(A2AConnection entry)
    {
        var result = await _manager.ValidateAsync(entry);

        // The editor refuses an endpoint or OAuth2 token endpoint on the site's own network; a recipe is held to the
        // same rule (review 11, C13).
        if (AIRecipeEndpoints.FindRefused(entry, _endpointPolicy) is { } refused)
        {
            result.Fail(new System.ComponentModel.DataAnnotations.ValidationResult(S["The endpoint '{0}' is on the site's own network. Use a public address, or ask the host to allow a private network for AI connections.", refused]));
        }

        return result;
    }

    protected override ValueTask CreateAsync(A2AConnection entry)
        => _manager.CreateAsync(entry);
}
