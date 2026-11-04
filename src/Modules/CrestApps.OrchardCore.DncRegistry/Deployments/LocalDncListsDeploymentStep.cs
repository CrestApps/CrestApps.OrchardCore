using CrestApps.OrchardCore.DncRegistry.Recipes;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.DncRegistry.Deployments;

/// <summary>
/// Exports every imported local do-not-call list, with its phone numbers, as a <c>LocalDncLists</c> recipe step. Phone numbers are personal data: add the step to a plan only to move them to a site you trust.
/// </summary>
public sealed class LocalDncListsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<LocalDncListsDeploymentStep>("Do Not Call");
    private static readonly LocalizationSource _title = LocalizationSource.Create<LocalDncListsDeploymentStep>("Local DNC lists");

    /// <inheritdoc/>
    public override string Name => LocalDncListsStep.StepKey;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
