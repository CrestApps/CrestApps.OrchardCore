using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Learning.Certifications.Recipes;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using CrestApps.OrchardCore.Skills.Services;
using Json.Schema;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Learning.Certifications.Deployments;

/// <summary>
/// Exports every certification path, with the name of its certification so another site matches it by name.
/// </summary>
public sealed class LearningCertificationPathsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<LearningCertificationPathsDeploymentStep>("Learning");
    private static readonly LocalizationSource _title = LocalizationSource.Create<LearningCertificationPathsDeploymentStep>("Learning certification paths");

    /// <inheritdoc/>
    public override string Name => CertificationsConstants.RecipeStepName;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class LearningCertificationPathsDeploymentSource : DeploymentSourceBase<LearningCertificationPathsDeploymentStep>
{
    private readonly ICertificationPathService _paths;
    private readonly ICertificationManager _certifications;

    public LearningCertificationPathsDeploymentSource(ICertificationPathService paths, ICertificationManager certifications)
    {
        _paths = paths;
        _certifications = certifications;
    }

    protected override async Task ProcessAsync(LearningCertificationPathsDeploymentStep step, DeploymentPlanResult result)
    {
        var rows = new JsonArray();

        foreach (var path in await _paths.ListAsync())
        {
            var node = JsonSerializer.SerializeToNode(path, LearningCertificationPathsRecipeStep.JsonOptions).AsObject();

            node["certificationName"] = (await _certifications.FindByIdAsync(path.CertificationId))?.Name;
            rows.Add(node);
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["Paths"] = rows,
        });
    }
}

internal sealed class LearningCertificationPathsDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, LearningCertificationPathsDeploymentStep>
{
    public override Task<IDisplayResult> DisplayAsync(LearningCertificationPathsDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("LearningCertificationPathsDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("LearningCertificationPathsDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(LearningCertificationPathsDeploymentStep step, BuildEditorContext context)
        => View("LearningCertificationPathsDeploymentStep_Fields_Edit", step).Location("Content");
}

internal sealed class LearningCertificationPathsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(LearningCertificationPathsDeploymentStep);

    protected override string DisplayText => "Learning certification paths";

    protected override string Description => "Exports every certification path with the name of its certification.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
