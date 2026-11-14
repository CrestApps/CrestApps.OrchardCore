using CrestApps.OrchardCore.Approvals.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Approvals.ViewModels;

/// <summary>
/// The editor of an approval policy.
/// </summary>
public class ApprovalPolicyViewModel
{
    /// <summary>
    /// The number of approver rule rows the editor shows.
    /// </summary>
    public const int RuleSlots = 4;

    /// <summary>
    /// Gets or sets the technical name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets how many approvals are required.
    /// </summary>
    public int RequiredApprovals { get; set; } = 1;

    /// <summary>
    /// Gets or sets a value indicating whether the requester may approve.
    /// </summary>
    public bool AllowRequesterToApprove { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a person may decide a request about themselves.
    /// </summary>
    public bool AllowSubjectToDecide { get; set; }

    /// <summary>
    /// Gets or sets the hours after which a request is due, as text.
    /// </summary>
    public string DueAfterHours { get; set; }

    /// <summary>
    /// Gets or sets the approver rules; an empty row is ignored.
    /// </summary>
    public List<ApproverRuleViewModel> Approvers { get; set; } = [];

    /// <summary>
    /// Gets or sets the kinds of approver rules.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> KindOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the policy is new.
    /// </summary>
    [BindNever]
    public bool IsNew { get; set; }
}

/// <summary>
/// One approver rule row of the policy editor.
/// </summary>
public class ApproverRuleViewModel
{
    /// <summary>
    /// Gets or sets the kind, or empty for an unused row.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the permission name, role name or staff ids.
    /// </summary>
    public string Value { get; set; }
}

/// <summary>
/// The list of approval requests.
/// </summary>
public class ApprovalRequestsIndexViewModel
{
    /// <summary>
    /// Gets or sets the status filter; pending by default.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the list.
    /// </summary>
    [BindNever]
    public AdminListContext<ApprovalRequest> List { get; set; }
}

/// <summary>
/// The page that shows a request and takes a decision.
/// </summary>
public class ApprovalDecideViewModel
{
    /// <summary>
    /// Gets or sets the approver's note.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets the request.
    /// </summary>
    [BindNever]
    public ApprovalRequest Request { get; set; }

    /// <summary>
    /// Gets or sets whether the person may decide, and why not.
    /// </summary>
    [BindNever]
    public ApprovalEligibility Eligibility { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the person may cancel it.
    /// </summary>
    [BindNever]
    public bool CanCancel { get; set; }

    /// <summary>
    /// Gets or sets the request's creation time, in the person's time zone.
    /// </summary>
    [BindNever]
    public DateTimeOffset CreatedLocal { get; set; }

    /// <summary>
    /// Gets or sets the due time, in the person's time zone.
    /// </summary>
    [BindNever]
    public DateTimeOffset? DueLocal { get; set; }

    /// <summary>
    /// Gets or sets each decision's time, in the person's time zone.
    /// </summary>
    [BindNever]
    public IList<DateTimeOffset> DecisionTimes { get; set; } = [];
}
