using System.Text.Json.Serialization;
using CrestApps.OrchardCore.DataConnectors.Recipes;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.DataConnectors.Deployments;

// Category and Title are marked JsonIgnore again: System.Text.Json reads the attributes of the override, not of the base
// property, and a plan that serialized them would fail to save.

/// <summary>
/// Represents a deployment step that exports data connections, without their secrets.
/// </summary>
public sealed class DataConnectionDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<DataConnectionDeploymentStep>("Data");
    private static readonly LocalizationSource _title = LocalizationSource.Create<DataConnectionDeploymentStep>("Data Connections");

    /// <inheritdoc />
    public override string Name => DataConnectionStep.StepKey;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;

    /// <summary>
    /// Gets or sets a value indicating whether every connection is exported.
    /// </summary>
    public bool IncludeAll { get; set; } = true;

    /// <summary>
    /// Gets or sets the identifiers of the connections exported when <see cref="IncludeAll"/> is off.
    /// </summary>
    public string[] ItemIds { get; set; } = [];
}

/// <summary>
/// Represents a deployment step that exports connected data sets, without the rows they imported.
/// </summary>
public sealed class ConnectedDataSetDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<ConnectedDataSetDeploymentStep>("Data");
    private static readonly LocalizationSource _title = LocalizationSource.Create<ConnectedDataSetDeploymentStep>("Connected Data Sets");

    /// <inheritdoc />
    public override string Name => ConnectedDataSetStep.StepKey;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;

    /// <summary>
    /// Gets or sets a value indicating whether every data set is exported.
    /// </summary>
    public bool IncludeAll { get; set; } = true;

    /// <summary>
    /// Gets or sets the identifiers of the data sets exported when <see cref="IncludeAll"/> is off.
    /// </summary>
    public string[] ItemIds { get; set; } = [];
}
