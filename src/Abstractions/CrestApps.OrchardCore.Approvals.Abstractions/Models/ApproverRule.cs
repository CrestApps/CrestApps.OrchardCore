namespace CrestApps.OrchardCore.Approvals.Models;

/// <summary>
/// One way to be an approver of a policy.
/// </summary>
public sealed class ApproverRule
{
    /// <summary>
    /// Gets or sets who the rule names.
    /// </summary>
    public ApproverKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the permission name, the role name, or the staff member ids separated by commas; empty for a
    /// manager.
    /// </summary>
    public string Value { get; set; }
}
