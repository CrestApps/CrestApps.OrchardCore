using System.Text.Json.Serialization;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Approvals.Deployments;

/// <summary>
/// Exports approval requests, for a site that takes over this one; imported requests are history.
/// </summary>
public sealed class ApprovalRequestsDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<ApprovalRequestsDeploymentStep>("Approvals");
    private static readonly LocalizationSource _title = LocalizationSource.Create<ApprovalRequestsDeploymentStep>("Approval requests");

    /// <inheritdoc />
    public override string Name => ApprovalsConstants.RequestsRecipeStepName;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [JsonIgnore]
    public override LocalizationSource Title => _title;
}
