using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Assessments.Core.Services;
using CrestApps.OrchardCore.Learning.Assessments.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Learning.Assessments.Deployments;

/// <summary>
/// Writes the grade scales step.
/// </summary>
internal sealed class LearningGradeScalesDeploymentSource : DeploymentSourceBase<LearningGradeScalesDeploymentStep>
{
    private readonly GradeScaleStore _store;

    public LearningGradeScalesDeploymentSource(GradeScaleStore store)
    {
        _store = store;
    }

    protected override async Task ProcessAsync(LearningGradeScalesDeploymentStep step, DeploymentPlanResult result)
    {
        var rows = new JsonArray();

        foreach (var scale in await _store.GetAllAsync())
        {
            rows.Add(JsonSerializer.SerializeToNode(scale, LearningGradeScalesRecipeStep.JsonOptions));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["GradeScales"] = rows,
        });
    }
}
