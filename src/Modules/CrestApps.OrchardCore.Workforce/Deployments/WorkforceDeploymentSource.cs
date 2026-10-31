using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Workforce.Recipes;
using CrestApps.OrchardCore.Workforce.Services;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Workforce.Deployments;

/// <summary>
/// Writes the Workforce step: activity codes and teams (with their parent by name), and the workers when asked (with
/// their teams by name and without sensitive details).
/// </summary>
internal sealed class WorkforceDeploymentSource : DeploymentSourceBase<WorkforceDeploymentStep>
{
    private const int PageSize = 500;
    private const int MaxRecords = 50_000;

    private readonly IActivityCodeManager _activityCodeManager;
    private readonly ITeamManager _teamManager;
    private readonly IWorkerManager _workerManager;

    public WorkforceDeploymentSource(
        IActivityCodeManager activityCodeManager,
        ITeamManager teamManager,
        IWorkerManager workerManager)
    {
        _activityCodeManager = activityCodeManager;
        _teamManager = teamManager;
        _workerManager = workerManager;
    }

    protected override async Task ProcessAsync(WorkforceDeploymentStep step, DeploymentPlanResult result)
    {
        var tree = await _teamManager.ListTreeAsync();
        var names = tree.ToDictionary(item => item.Team.ItemId, item => item.Team.Name, StringComparer.Ordinal);

        var node = new JsonObject
        {
            ["name"] = step.Name,
            ["ActivityCodes"] = new JsonArray([.. (await _activityCodeManager.GetAllAsync()).Select(code => Serialize(code))]),
            ["Teams"] = new JsonArray([.. tree.Select(item =>
            {
                var team = Serialize(item.Team, "calendarId", "parentId");

                if (item.Team.ParentId is { } parentId && names.TryGetValue(parentId, out var parentName))
                {
                    team["parentName"] = parentName;
                }

                return team;
            })]),
        };

        if (step.IncludeWorkers)
        {
            var workers = new JsonArray();

            for (var skip = 0; skip < MaxRecords; skip += PageSize)
            {
                var (_, page) = await _workerManager.SearchAsync(new WorkerQuery(), skip, PageSize);

                foreach (var worker in page)
                {
                    var item = Serialize(worker, "dateOfBirth", "primaryTeamId", "managerWorkerId");

                    if (item["teamMemberships"] is JsonArray memberships)
                    {
                        foreach (var membership in memberships.OfType<JsonObject>())
                        {
                            if (membership["teamId"]?.GetValue<string>() is { } teamId && names.TryGetValue(teamId, out var teamName))
                            {
                                membership["teamName"] = teamName;
                            }

                            membership.Remove("teamId");
                        }
                    }

                    workers.Add(item);
                }

                if (page.Count < PageSize)
                {
                    break;
                }
            }

            node["Workers"] = workers;
        }

        result.Steps.Add(node);
    }

    private static JsonObject Serialize<T>(T record, params string[] without)
    {
        var node = JsonSerializer.SerializeToNode(record, WorkforceRecipeStep.JsonOptions).AsObject();

        node.Remove("id");
        node.Remove("createdUtc");
        node.Remove("modifiedUtc");

        foreach (var name in without)
        {
            node.Remove(name);
        }

        return node;
    }
}
