using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.TenantHierarchy.Deployments;

/// <summary>
/// Exports who in the parent tenant may sign in to child tenants, and with which roles, as a <c>TenantHierarchyAccessGrants</c> recipe step. Users and roles are written by name and child tenants by entry id and slug.
/// </summary>
public sealed class AccessGrantsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<AccessGrantsDeploymentStep>("Tenants");
    private static readonly LocalizationSource _title = LocalizationSource.Create<AccessGrantsDeploymentStep>("Child tenant access");

    /// <inheritdoc/>
    public override string Name => TenantHierarchyConstants.Recipes.AccessGrants;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
