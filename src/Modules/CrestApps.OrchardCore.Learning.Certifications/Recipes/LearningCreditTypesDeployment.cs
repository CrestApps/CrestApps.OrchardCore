using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Learning.Certifications.Recipes;

/// <summary>
/// Exports every education credit type; credits people earned are learner records and are not exported.
/// </summary>
public sealed class LearningCreditTypesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<LearningCreditTypesDeploymentStep>("Learning");
    private static readonly LocalizationSource _title = LocalizationSource.Create<LearningCreditTypesDeploymentStep>("Education credit types");

    /// <inheritdoc/>
    public override string Name => CertificationsConstants.CreditTypesRecipeStepName;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}

internal sealed class LearningCreditTypesDeploymentSource : DeploymentSourceBase<LearningCreditTypesDeploymentStep>
{
    private readonly IEducationCreditService _credits;

    public LearningCreditTypesDeploymentSource(IEducationCreditService credits)
    {
        _credits = credits;
    }

    protected override async Task ProcessAsync(LearningCreditTypesDeploymentStep step, DeploymentPlanResult result)
    {
        var rows = new JsonArray();

        foreach (var type in await _credits.ListTypesAsync())
        {
            rows.Add(JsonSerializer.SerializeToNode(type, LearningCertificationPathsRecipeStep.JsonOptions));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = step.Name,
            ["CreditTypes"] = rows,
        });
    }
}

internal sealed class LearningCreditTypesDeploymentStepDisplayDriver : DisplayDriver<DeploymentStep, LearningCreditTypesDeploymentStep>
{
    internal readonly IStringLocalizer S;

    public LearningCreditTypesDeploymentStepDisplayDriver(IStringLocalizer<LearningCreditTypesDeploymentStepDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(LearningCreditTypesDeploymentStep step, BuildDisplayContext context)
        => CombineAsync(
            View("LearningCreditTypesDeploymentStep_Summary", step).Location("Summary", "Content"),
            View("LearningCreditTypesDeploymentStep_Thumbnail", step).Location("Thumbnail", "Content"));

    public override IDisplayResult Edit(LearningCreditTypesDeploymentStep step, BuildEditorContext context)
        => View("LearningCreditTypesDeploymentStep_Fields_Edit", step).Location(l => l.Zone("Content", "1").Card(S["Education Credit Types"], "10"));
}

internal sealed class LearningCreditTypesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(LearningCreditTypesDeploymentStep);

    protected override string DisplayText => "Education credit types";

    protected override string Description => "Exports every education credit type.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
