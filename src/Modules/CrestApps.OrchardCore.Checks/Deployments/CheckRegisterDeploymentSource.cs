using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Checks.Core.Indexes;
using CrestApps.OrchardCore.Checks.Models;
using CrestApps.OrchardCore.Checks.Recipes;
using OrchardCore.Deployment;
using YesSql;

namespace CrestApps.OrchardCore.Checks.Deployments;

/// <summary>
/// Writes the check register to a deployment plan, a page at a time.
/// </summary>
internal sealed class CheckRegisterDeploymentSource : DeploymentSourceBase<CheckRegisterDeploymentStep>
{
    private const int PageSize = 500;

    private readonly ISession _session;

    public CheckRegisterDeploymentSource(ISession session)
    {
        _session = session;
    }

    protected override async Task ProcessAsync(CheckRegisterDeploymentStep step, DeploymentPlanResult result)
    {
        var checks = new JsonArray();

        for (var skip = 0; ; skip += PageSize)
        {
            var page = (await _session.Query<CheckRecord, CheckRecordIndex>(collection: ChecksConstants.CollectionName)
                .OrderBy(index => index.IssuedUtc)
                .ThenBy(index => index.Number)
                .Skip(skip)
                .Take(PageSize)
                .ListAsync()).ToList();

            foreach (var check in page)
            {
                var node = JsonSerializer.SerializeToNode(check, CheckRegisterRecipeStep.JsonOptions).AsObject();

                node.Remove("id");
                node.Remove("properties");
                checks.Add(node);
            }

            if (page.Count < PageSize)
            {
                break;
            }
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            [CheckRegisterRecipeStep.ChecksProperty] = checks,
        });
    }
}
