namespace CrestApps.OrchardCore.Approvals.Models;

/// <summary>
/// Who an approver rule names.
/// </summary>
public enum ApproverKind
{
    /// <summary>
    /// Anyone who holds a permission (the rule's value is the permission name).
    /// </summary>
    Permission,

    /// <summary>
    /// Anyone in a role (the rule's value is the role name).
    /// </summary>
    Role,

    /// <summary>
    /// A manager of the staff member the request is about, through the staff hierarchy.
    /// </summary>
    Manager,

    /// <summary>
    /// Named staff members (the rule's value is their ids, separated by commas).
    /// </summary>
    Staff,
}
