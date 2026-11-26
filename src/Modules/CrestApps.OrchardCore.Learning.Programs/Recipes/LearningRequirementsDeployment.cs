using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Learning.Programs.Services;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Learning.Programs.Recipes;

/// <summary>
/// Exports every required learning rule; the states of people are learner records and are not exported.
/// </summary>
public sealed class LearningRequirementsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<LearningRequirementsDeploymentStep>("Learning");
    private static readonly LocalizationSource _title = LocalizationSource.Create<LearningRequirementsDeploymentStep>("Required learning");

    /// <inheritdoc/>
    public override string Name => ProgramsConstants.RecipeStepName;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class LearningRequirementsDeploymentSource : DeploymentSourceBase<LearningRequirementsDeploymentStep>
{
    private readonly IRequiredLearningService _requiredLearning;

    public LearningRequirementsDeploymentSource(IRequiredLearningService requiredLearning)
    {
        _requiredLearning = requiredLearning;
    }

    protected override async Task ProcessAsync(LearningRequirementsDeploymentStep step, DeploymentPlanResult result)
    {
        var rows = new JsonArray();

        foreach (var rule in await _requiredLearning.ListAsync())
        {
            var node = JsonSerializer.SerializeToNode(rule, LearningRequirementsRecipeStep.JsonOptions).AsObject();

            node.Remove("lastRunUtc");
            node.Remove("lastRunCovered");
            rows.Add(node);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["Requirements"] = rows,
        });
    }
}

internal sealed class LearningRequirementsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, LearningRequirementsDeploymentStep>
{
    internal readonly IStringLocalizer S;

    public LearningRequirementsDeploymentStepDisplayDriver(IStringLocalizer<LearningRequirementsDeploymentStepDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(LearningRequirementsDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("LearningRequirementsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("LearningRequirementsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(LearningRequirementsDeploymentStep step, BuildEditorContext context)
        => View("LearningRequirementsDeploymentStep_Fields_Edit", step).Location(l => l.Zone("Content", "1").Card(S["Required Learning"], "10"));
}

internal sealed class LearningRequirementsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(LearningRequirementsDeploymentStep);

    protected override string DisplayText => "Required learning";

    protected override string Description => "Exports every required learning rule, without the states of people.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
