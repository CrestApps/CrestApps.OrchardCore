using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace CrestApps.OrchardCore.Workforce.Recipes;

/// <summary>
/// The WorkforceTimeOff recipe step: time-off types, policies, allotments and blackout dates, with their types and
/// teams by name.
/// </summary>
internal sealed class TimeOffRecipeStep : NamedRecipeStepHandler
{
    public const string TimeOffStepName = "WorkforceTimeOff";

    private readonly ITimeOffTypeManager _typeManager;
    private readonly ITimeOffPolicyManager _policyManager;
    private readonly ITimeOffAllotmentManager _allotmentManager;
    private readonly IBlackoutPeriodManager _blackoutManager;
    private readonly ITeamManager _teamManager;

    public TimeOffRecipeStep(
        ITimeOffTypeManager typeManager,
        ITimeOffPolicyManager policyManager,
        ITimeOffAllotmentManager allotmentManager,
        IBlackoutPeriodManager blackoutManager,
        ITeamManager teamManager)
        : base(TimeOffStepName)
    {
        _typeManager = typeManager;
        _policyManager = policyManager;
        _allotmentManager = allotmentManager;
        _blackoutManager = blackoutManager;
        _teamManager = teamManager;
    }

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var import = new RecipeImport(context, TimeOffStepName);
        var teams = (await _teamManager.GetAllAsync()).ToList();
        var types = (await _typeManager.GetAllAsync()).ToList();

        foreach (var (_, incoming) in import.Read<TimeOffType>("Types"))
        {
            var saved = await import.SaveAsync(_typeManager, incoming, RecipeImport.Existing(types, incoming, item => item.Name), $"time-off type '{incoming.Name}'");

            if (saved is not null && !types.Contains(saved))
            {
                types.Add(saved);
            }
        }

        var policies = (await _policyManager.GetAllAsync()).ToList();

        foreach (var (node, incoming) in import.Read<TimeOffPolicy>("Policies"))
        {
            var ok = true;

            if (RecipeImport.String(node, "typeName") is { } typeName)
            {
                var type = import.Named(types, item => item.Name, typeName, "policy", incoming.Name, "a time-off type");

                ok &= type is not null;
                incoming.TypeId = type?.ItemId;
            }

            foreach (var teamName in RecipeImport.Strings(node, "teamNames"))
            {
                var team = import.Named(teams, item => item.Name, teamName, "policy", incoming.Name, "a team");

                ok &= team is not null;

                if (team is not null && incoming.Assignments.All(assignment => assignment.TeamId != team.ItemId))
                {
                    incoming.Assignments.Add(new TimeOffPolicyAssignment { TeamId = team.ItemId });
                }
            }

            if (ok && await import.SaveAsync(_policyManager, incoming, RecipeImport.Existing(policies, incoming, item => item.Name), $"policy '{incoming.Name}'") is { } saved && !policies.Contains(saved))
            {
                policies.Add(saved);
            }
        }

        var allotments = (await _allotmentManager.GetAllAsync()).ToList();

        foreach (var (node, incoming) in import.Read<TimeOffAllotment>("Allotments"))
        {
            if (Resolve(import, node, "allotment", incoming.Name, teams, types, id => incoming.TeamId = id, incoming.TypeIds)
                && await import.SaveAsync(_allotmentManager, incoming, RecipeImport.Existing(allotments, incoming, item => item.Name), $"allotment '{incoming.Name}'") is { } saved
                && !allotments.Contains(saved))
            {
                allotments.Add(saved);
            }
        }

        var blackouts = (await _blackoutManager.GetAllAsync()).ToList();

        foreach (var (node, incoming) in import.Read<BlackoutPeriod>("BlackoutPeriods"))
        {
            if (Resolve(import, node, "blackout period", incoming.Name, teams, types, id => incoming.TeamId = id, incoming.TypeIds)
                && await import.SaveAsync(_blackoutManager, incoming, RecipeImport.Existing(blackouts, incoming, item => item.Name), $"blackout period '{incoming.Name}'") is { } saved
                && !blackouts.Contains(saved))
            {
                blackouts.Add(saved);
            }
        }
    }

    private static bool Resolve(RecipeImport import, System.Text.Json.Nodes.JsonObject node, string owner, string name, IReadOnlyList<WorkforceTeam> teams, IReadOnlyList<TimeOffType> types, Action<string> setTeam, IList<string> typeIds)
    {
        var ok = true;

        if (RecipeImport.String(node, "teamName") is { } teamName)
        {
            var team = import.Named(teams, item => item.Name, teamName, owner, name, "a team");

            ok &= team is not null;
            setTeam(team?.ItemId);
        }

        foreach (var typeName in RecipeImport.Strings(node, "typeNames"))
        {
            var type = import.Named(types, item => item.Name, typeName, owner, name, "a time-off type");

            ok &= type is not null;

            if (type is not null && !typeIds.Contains(type.ItemId))
            {
                typeIds.Add(type.ItemId);
            }
        }

        return ok;
    }
}
