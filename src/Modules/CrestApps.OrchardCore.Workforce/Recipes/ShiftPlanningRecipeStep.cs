using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Workforce.Recipes;

/// <summary>
/// The WorkforceShiftPlanning recipe step: shift templates, with their activities and teams by name.
/// </summary>
internal sealed class ShiftPlanningRecipeStep : NamedRecipeStepHandler
{
    public const string ShiftPlanningStepName = "WorkforceShiftPlanning";

    private readonly IShiftTemplateManager _templateManager;
    private readonly IActivityCodeManager _activityCodeManager;
    private readonly ITeamManager _teamManager;

    public ShiftPlanningRecipeStep(IShiftTemplateManager templateManager, IActivityCodeManager activityCodeManager, ITeamManager teamManager)
        : base(ShiftPlanningStepName)
    {
        _templateManager = templateManager;
        _activityCodeManager = activityCodeManager;
        _teamManager = teamManager;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var import = new RecipeImport(context, ShiftPlanningStepName);
        var templates = (await _templateManager.GetAllAsync()).ToList();
        var codes = (await _activityCodeManager.GetAllAsync()).ToList();
        var teams = (await _teamManager.GetAllAsync()).ToList();

        foreach (var (node, incoming) in import.Read<ShiftTemplate>("Templates"))
        {
            var ok = true;

            foreach (var teamName in RecipeImport.Strings(node, "teamNames"))
            {
                var team = import.Named(teams, item => item.Name, teamName, "template", incoming.Name, "a team");

                ok &= team is not null;

                if (team is not null && !incoming.TeamIds.Contains(team.ItemId))
                {
                    incoming.TeamIds.Add(team.ItemId);
                }
            }

            var segments = (node["segments"] as System.Text.Json.Nodes.JsonArray ?? []).OfType<System.Text.Json.Nodes.JsonObject>().ToArray();

            for (var index = 0; index < segments.Length && index < incoming.Segments.Count; index++)
            {
                if (RecipeImport.String(segments[index], "activityName") is { } activityName)
                {
                    var code = import.Named(codes, item => item.Name, activityName, "template", incoming.Name, "an activity");

                    ok &= code is not null;
                    incoming.Segments[index].ActivityCodeId = code?.ItemId;
                }
            }

            if (!ok)
            {
                continue;
            }

            var saved = await import.SaveAsync(_templateManager, incoming, RecipeImport.Existing(templates, incoming, item => item.Name), $"template '{incoming.Name}'");

            if (saved is not null && !templates.Contains(saved))
            {
                templates.Add(saved);
            }
        }
    }
}
