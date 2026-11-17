namespace CrestApps.OrchardCore.Approvals.Models;

/// <summary>
/// One step of a policy: who may decide it and how many of them must approve before the next step starts. A request
/// is approved when its last stage is; one rejection at any stage rejects it.
/// </summary>
public sealed class ApprovalStage
{
    /// <summary>
    /// Gets or sets what people see, such as "Manager" or "Finance".
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the ways to be an approver of the stage; anyone any rule names may approve.
    /// </summary>
    public List<ApproverRule> Approvers { get; set; } = [];

    /// <summary>
    /// Gets or sets how many different approvers must approve the stage, 1 or more.
    /// </summary>
    public int RequiredApprovals { get; set; } = 1;

    /// <summary>
    /// Returns a copy.
    /// </summary>
    /// <returns>The copy.</returns>
    public ApprovalStage Clone()
        => new()
        {
            Title = Title,
            Approvers = [.. (Approvers ?? []).Select(rule => rule.Clone())],
            RequiredApprovals = RequiredApprovals,
        };
}
