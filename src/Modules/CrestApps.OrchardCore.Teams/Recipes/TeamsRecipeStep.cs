using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Teams.Core;
using CrestApps.OrchardCore.Teams.Services;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Teams.Recipes;

/// <summary>
/// Imports the Teams step: teams created, or updated by id or name, with the parent by <c>parentName</c> in any order.
/// Every team goes through the team manager, so it is validated and the handlers of the modules that use teams run.
/// </summary>
internal sealed class TeamsRecipeStep : NamedRecipeStepHandler
{
    private readonly ITeamManager _teamManager;

    public TeamsRecipeStep(ITeamManager teamManager)
        : base(TeamsConstants.RecipeStepName)
    {
        _teamManager = teamManager;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var nodes = (context.Step["Teams"] as JsonArray ?? []).OfType<JsonObject>();

        await TeamRecipeImport.ImportAsync(_teamManager, nodes, context.Errors, TeamsConstants.RecipeStepName);
    }
}
