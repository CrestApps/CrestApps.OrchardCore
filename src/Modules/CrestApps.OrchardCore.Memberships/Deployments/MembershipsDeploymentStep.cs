using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Memberships.Deployments;

/// <summary>
/// Exports every membership, with its member by user name, as a <c>Memberships</c> recipe step. The member's user id and the roles the membership put them in belong to the site, so they are left out.
/// </summary>
public sealed class MembershipsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<MembershipsDeploymentStep>("Commerce");
    private static readonly LocalizationSource _title = LocalizationSource.Create<MembershipsDeploymentStep>("Memberships");

    /// <inheritdoc/>
    public override string Name => MembershipsConstants.Recipes.Memberships;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc/>
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
