using System.Text.Json.Nodes;
using CrestApps.Core.AI.A2A.Models;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
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

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="A2AConnectionStep"/> class.
    /// </summary>
    /// <param name="manager">The connection manager.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public A2AConnectionStep(
        ICatalogManager<A2AConnection> manager,
        IStringLocalizer<A2AConnectionStep> stringLocalizer)
        : base(StepKey, "Connections")
    {
        _manager = manager;
        S = stringLocalizer;
    }

    protected override ValueTask<A2AConnection> FindByIdAsync(string id)
        => _manager.FindByIdAsync(id);

    protected override ValueTask UpdateAsync(A2AConnection entry, JsonObject token)
        => _manager.UpdateAsync(entry, token);

    protected override ValueTask<A2AConnection> NewAsync(JsonObject token, RecipeExecutionContext context)
        => _manager.NewAsync(token);

    protected override ValueTask<ValidationResultDetails> ValidateAsync(A2AConnection entry)
        => _manager.ValidateAsync(entry);

    protected override ValueTask CreateAsync(A2AConnection entry)
        => _manager.CreateAsync(entry);
}
