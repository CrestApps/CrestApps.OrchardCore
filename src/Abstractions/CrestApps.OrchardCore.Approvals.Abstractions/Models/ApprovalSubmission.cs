namespace CrestApps.OrchardCore.Approvals.Models;

/// <summary>
/// What a module or a workflow asks approval for.
/// </summary>
public sealed class ApprovalSubmission
{
    /// <summary>
    /// Gets or sets the policy, by id or technical name; empty to use the first enabled policy whose types and
    /// conditions match (<c>IApprovalPolicyResolver</c>).
    /// </summary>
    public string Policy { get; set; }

    /// <summary>
    /// Gets or sets the approval type's name, the kind of the subject, such as <c>PayRun</c>.
    /// </summary>
    public string SubjectKind { get; set; }

    /// <summary>
    /// Gets or sets the subject's id.
    /// </summary>
    public string SubjectId { get; set; }

    /// <summary>
    /// Gets or sets the access scope within the subject kind.
    /// </summary>
    public string AccessScope { get; set; }

    /// <summary>
    /// Gets or sets the subject's page, application-relative.
    /// </summary>
    public string SubjectUrl { get; set; }

    /// <summary>
    /// Gets or sets the user ids of the people the subject is about.
    /// </summary>
    public IList<string> SubjectUserIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the staff member the subject is about.
    /// </summary>
    public string SubjectStaffMemberId { get; set; }

    /// <summary>
    /// Gets or sets what is asked.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the amount, when there is one.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency of the amount.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the version of the subject, such as a fingerprint of its totals. A pending request about another
    /// version is withdrawn and replaced.
    /// </summary>
    public string SubjectVersion { get; set; }
}
