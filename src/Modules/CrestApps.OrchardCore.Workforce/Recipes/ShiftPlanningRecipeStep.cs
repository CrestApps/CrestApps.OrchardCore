using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Workforce.Recipes;

/// <summary>
/// The WorkforceShiftPlanning recipe step: shift templates, with their activities and teams by name; rotations
/// (shift patterns), with their team and templates by name; and coverage requirements, with their team and skill by
/// name.
/// </summary>
internal sealed class ShiftPlanningRecipeStep : NamedRecipeStepHandler
{
    public const string ShiftPlanningStepName = "WorkforceShiftPlanning";

    private readonly IShiftTemplateManager _templateManager;
    private readonly IActivityCodeManager _activityCodeManager;
    private readonly ITeamManager _teamManager;
    private readonly IShiftPatternManager _patternManager;
    private readonly ICoverageService _coverageService;
    private readonly ISkillManager _skillManager;

    public ShiftPlanningRecipeStep(
        IShiftTemplateManager templateManager,
        IActivityCodeManager activityCodeManager,
        ITeamManager teamManager,
        IShiftPatternManager patternManager,
        ICoverageService coverageService,
        ISkillManager skillManager)
        : base(ShiftPlanningStepName)
    {
        _templateManager = templateManager;
        _activityCodeManager = activityCodeManager;
        _teamManager = teamManager;
        _patternManager = patternManager;
        _coverageService = coverageService;
        _skillManager = skillManager;
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

            var segments = (node["segments"] as JsonArray ?? []).OfType<JsonObject>().ToArray();

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

        await ImportPatternsAsync(import, templates, teams);
        await ImportCoverageAsync(import, context, teams);
    }

    private async Task ImportPatternsAsync(RecipeImport import, List<ShiftTemplate> templates, List<Team> teams)
    {
        var patterns = (await _patternManager.GetAllAsync()).ToList();

        foreach (var (node, incoming) in import.Read<ShiftPattern>("Patterns"))
        {
            var ok = true;

            if (RecipeImport.String(node, "teamName") is { } teamName)
            {
                var team = import.Named(teams, item => item.Name, teamName, "rotation", incoming.Name, "a team");

                ok &= team is not null;
                incoming.TeamId = team?.ItemId;
            }

            var days = (node["days"] as JsonArray ?? []).OfType<JsonObject>().ToArray();

            for (var index = 0; index < days.Length && index < incoming.Days.Count; index++)
            {
                if (RecipeImport.String(days[index], "templateName") is { } templateName)
                {
                    var template = import.Named(templates, item => item.Name, templateName, "rotation", incoming.Name, "a shift template");

                    ok &= template is not null;
                    incoming.Days[index].TemplateId = template?.ItemId;
                }
            }

            if (!ok)
            {
                continue;
            }

            var saved = await import.SaveAsync(_patternManager, incoming, RecipeImport.Existing(patterns, incoming, item => item.Name), $"rotation '{incoming.Name}'");

            if (saved is not null && !patterns.Contains(saved))
            {
                patterns.Add(saved);
            }
        }
    }

    private async Task ImportCoverageAsync(RecipeImport import, RecipeExecutionContext context, List<Team> teams)
    {
        List<Skill> skills = null;

        foreach (var (node, incoming) in import.Read<CoverageRequirement>("CoverageRequirements"))
        {
            var ok = true;

            if (RecipeImport.String(node, "teamName") is { } teamName)
            {
                var team = import.Named(teams, item => item.Name, teamName, "coverage requirement", incoming.Name, "a team");

                ok &= team is not null;
                incoming.TeamId = team?.ItemId;
            }

            if (RecipeImport.String(node, "skillName") is { } skillName)
            {
                skills ??= (await _skillManager.GetAllAsync()).ToList();

                var skill = import.Named(skills, item => item.Name, skillName, "coverage requirement", incoming.Name, "a skill");

                ok &= skill is not null;
                incoming.SkillId = skill?.ItemId;
            }

            if (!ok)
            {
                continue;
            }

            var existing = !string.IsNullOrEmpty(incoming.ItemId)
                ? await _coverageService.FindRequirementAsync(incoming.ItemId)
                : null;

            if (existing is null && !string.IsNullOrEmpty(incoming.TeamId))
            {
                existing = (await _coverageService.ListRequirementsAsync(incoming.TeamId))
                    .FirstOrDefault(item => WorkforceNames.Normalize(item.Name) == WorkforceNames.Normalize(incoming.Name));
            }

            if (existing is not null)
            {
                incoming.ItemId = existing.ItemId;
            }

            var result = await _coverageService.SaveRequirementAsync(incoming);

            if (!result.Succeeded)
            {
                context.Errors.Add($"The coverage requirement '{incoming.Name}' in the {ShiftPlanningStepName} step was refused: {string.Join(" ", result.Errors)}");
            }
        }
    }
}
