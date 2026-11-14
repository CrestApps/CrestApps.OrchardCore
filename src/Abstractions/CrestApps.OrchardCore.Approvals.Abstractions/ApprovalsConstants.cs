namespace CrestApps.OrchardCore.Approvals;

/// <summary>
/// The names the Approvals module stores and registers.
/// </summary>
public static class ApprovalsConstants
{
    /// <summary>
    /// The YesSql collection of approval requests.
    /// </summary>
    public const string CollectionName = "Approvals";

    /// <summary>
    /// The recipe and deployment step of approval policies.
    /// </summary>
    public const string PoliciesRecipeStepName = "ApprovalPolicies";

    /// <summary>
    /// The recipe and deployment step of approval requests.
    /// </summary>
    public const string RequestsRecipeStepName = "ApprovalRequests";

    /// <summary>
    /// The notification type that asks an approver to decide, kept on notifications; never change it.
    /// </summary>
    public const string ApprovalNotificationType = "Approval";

    /// <summary>
    /// The notification type that tells the requester the decision, kept on notifications; never change it.
    /// </summary>
    public const string DecisionNotificationType = "ApprovalDecided";

    /// <summary>
    /// The feature ids of the module.
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// The Approvals feature.
        /// </summary>
        public const string Approvals = "CrestApps.OrchardCore.Approvals";
    }
}
