using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Approvals.Models;

/// <summary>
/// Who may decide a kind of approval and how many of them must approve. A request follows the policy it was made
/// under; the two built-in rules (the requester can't approve, nobody decides a request about their own record) can
/// each be turned off, such as for a one-person business.
/// </summary>
public sealed class ApprovalPolicy : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the policy's technical name, used by modules and workflows.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the title shown to people.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets what the policy is for.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the ways to be an approver; anyone any rule names may approve.
    /// </summary>
    public List<ApproverRule> Approvers { get; set; } = [];

    /// <summary>
    /// Gets or sets how many different approvers must approve, 1 or more. One rejection rejects the request.
    /// </summary>
    public int RequiredApprovals { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether the person who asked may also approve.
    /// </summary>
    public bool AllowRequesterToApprove { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a person may decide a request about their own record.
    /// </summary>
    public bool AllowSubjectToDecide { get; set; }

    /// <summary>
    /// Gets or sets how many hours after it is made a request is due, or <see langword="null"/>.
    /// </summary>
    public int? DueAfterHours { get; set; }

    /// <summary>
    /// Gets or sets when the policy last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
