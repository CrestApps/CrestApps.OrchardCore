using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Teams.Core;
using CrestApps.OrchardCore.Teams.Services;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Teams.Deployments;

/// <summary>
/// Writes the Teams step: every team in tree order, with its parent by name. What other modules keep on a team is left
/// to their own steps, because it can name records of this site, such as a calendar.
/// </summary>
internal sealed class TeamsDeploymentSource : DeploymentSourceBase<TeamsDeploymentStep>
{
    private readonly ITeamManager _teamManager;

    public TeamsDeploymentSource(ITeamManager teamManager)
    {
        _teamManager = teamManager;
    }

    protected override async Task ProcessAsync(TeamsDeploymentStep step, DeploymentPlanResult result)
    {
        var tree = await _teamManager.ListTreeAsync();
        var names = tree.ToDictionary(item => item.Team.ItemId, item => item.Team.Name, StringComparer.Ordinal);

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["Teams"] = new JsonArray([.. tree.Select(item =>
            {
                var team = JsonSerializer.SerializeToNode(item.Team, TeamRecipeImport.JsonOptions).AsObject();

                team.Remove("id");
                team.Remove("createdUtc");
                team.Remove("modifiedUtc");
                team.Remove("parentId");
                team.Remove("properties");

                if (item.Team.ParentId is { } parentId && names.TryGetValue(parentId, out var parentName))
                {
                    team["parentName"] = parentName;
                }

                return (JsonNode)team;
            })]),
        });
    }
}
