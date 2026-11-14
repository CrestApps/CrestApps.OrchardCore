using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Approvals;

/// <summary>
/// The permissions of approvals. Who may decide a request is the request's policy, not a permission.
/// </summary>
public static class ApprovalsPermissions
{
    /// <summary>
    /// Add, change and delete approval policies.
    /// </summary>
    public static readonly Permission ManageApprovalPolicies = new("ManageApprovalPolicies", LocalizationSource.Create("Manage approval policies", typeof(ApprovalsPermissions)), isSecurityCritical: true);

    /// <summary>
    /// See every approval request the source access allows and cancel any of them.
    /// </summary>
    public static readonly Permission ManageApprovalRequests = new("ManageApprovalRequests", LocalizationSource.Create("Manage approval requests", typeof(ApprovalsPermissions)), isSecurityCritical: true);

    /// <summary>
    /// Open the list of approval requests, to decide the ones the person is an approver of.
    /// </summary>
    public static readonly Permission ViewApprovalRequests = new("ViewApprovalRequests", LocalizationSource.Create("View approval requests", typeof(ApprovalsPermissions)), [ManageApprovalRequests]);
}
