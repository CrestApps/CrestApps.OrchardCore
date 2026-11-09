using CrestApps.OrchardCore.AI.FileSources.Recipes;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.AI.FileSources.Deployments;

/// <summary>
/// Exports every file source as a <c>FileSources</c> recipe step. Connection passwords, keys and other secrets are never exported.
/// </summary>
public sealed class FileSourcesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<FileSourcesDeploymentStep>("Artificial Intelligence");
    private static readonly LocalizationSource _title = LocalizationSource.Create<FileSourcesDeploymentStep>("File sources");

    /// <inheritdoc/>
    public override string Name => FileSourcesStep.StepKey;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
