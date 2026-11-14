using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Approvals.Deployments;

/// <summary>
/// Exports the approval policies.
/// </summary>
public sealed class ApprovalPoliciesDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<ApprovalPoliciesDeploymentStep>("Approvals");
    private static readonly LocalizationSource _title = LocalizationSource.Create<ApprovalPoliciesDeploymentStep>("Approval policies");

    /// <inheritdoc />
    public override string Name => ApprovalsConstants.PoliciesRecipeStepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}
