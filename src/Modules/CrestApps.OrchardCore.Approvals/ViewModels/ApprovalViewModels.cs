using CrestApps.OrchardCore.Approvals.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Approvals.ViewModels;

/// <summary>
/// The editor of an approval policy.
/// </summary>
public class ApprovalPolicyViewModel
{
    /// <summary>
    /// The most stages a policy can have; the editor and the save both hold to it.
    /// </summary>
    public const int MaxStages = 10;

    /// <summary>
    /// The most approver rules a stage can have; the editor and the save both hold to it.
    /// </summary>
    public const int MaxRules = 10;

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
    /// Gets or sets the policy's approval type as people see it.
    /// </summary>
    [BindNever]
    public string TypeDisplayName { get; set; }
    /// <summary>
    /// Gets or sets whether the policy is chosen for requests that name none.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the order among policies, as text.
    /// </summary>
    public string Order { get; set; }

    /// <summary>
    /// Gets or sets the stages.
    /// </summary>
    public List<ApprovalStageViewModel> Stages { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the requester may approve.
    /// </summary>
    public bool AllowRequesterToApprove { get; set; }

    /// <summary>
    /// Gets or sets whether a person may decide a request about themselves.
    /// </summary>
    public bool AllowSubjectToDecide { get; set; }

    /// <summary>
    /// Gets or sets whether one person may decide more than one stage.
    /// </summary>
    public bool AllowSamePersonAcrossStages { get; set; }

    /// <summary>
    /// Gets or sets the hours until a request is due, as text.
    /// </summary>
    public string DueAfterHours { get; set; }

    /// <summary>
    /// Gets or sets the approver rule providers to choose from, by name with what people see.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> KindOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the policy is new.
    /// </summary>
    [BindNever]
    public bool IsNew { get; set; }
}

/// <summary>
/// The standard conditions of a policy, the ones its approval type opts into.
/// </summary>
public class ApprovalPolicyConditionsViewModel
{
    /// <summary>
    /// Gets or sets the least amount, as text with a dot.
    /// </summary>
    public string MinimumAmount { get; set; }

    /// <summary>
    /// Gets or sets the most amount, as text with a dot.
    /// </summary>
    public string MaximumAmount { get; set; }

    /// <summary>
    /// Gets or sets the currency condition.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the access scope condition.
    /// </summary>
    public string AccessScope { get; set; }

    /// <summary>
    /// Gets or sets whether the amount condition shows.
    /// </summary>
    [BindNever]
    public bool ShowsAmount { get; set; }

    /// <summary>
    /// Gets or sets whether the scope condition shows.
    /// </summary>
    [BindNever]
    public bool ShowsScope { get; set; }

    /// <summary>
    /// Gets or sets the scopes to choose from; empty for a text box.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> ScopeOptions { get; set; } = [];
}

/// <summary>
/// One stage in the policy editor.
/// </summary>
public class ApprovalStageViewModel
{
    /// <summary>
    /// Gets or sets the stage's title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets how many approvals the stage needs.
    /// </summary>
    public int RequiredApprovals { get; set; } = 1;

    /// <summary>
    /// Gets or sets the approver rules.
    /// </summary>
    public List<ApproverRuleViewModel> Approvers { get; set; } = [];
}

/// <summary>
/// One approver rule row in the policy editor: the provider chosen, and each provider's value editor's value (only
/// the chosen provider's is kept).
/// </summary>
public class ApproverRuleViewModel
{
    /// <summary>
    /// Gets or sets the provider's name, or empty for an unused row.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the values posted by each provider's editor, by provider name; a multiple choice posts several.
    /// </summary>
    public Dictionary<string, string[]> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// The list of approval requests.
/// </summary>
public class ApprovalRequestsIndexViewModel
{
    /// <summary>
    /// The list of the requests waiting for the reader's decision.
    /// </summary>
    public const string WaitingView = "Waiting";

    /// <summary>
    /// The list of every request the reader may see.
    /// </summary>
    public const string AllView = "All";

    /// <summary>
    /// The list of the requests the reader made.
    /// </summary>
    public const string MineView = "Mine";

    /// <summary>
    /// Gets or sets which list: <see cref="WaitingView"/>, <see cref="AllView"/> or <see cref="MineView"/>.
    /// </summary>
    public string View { get; set; }

    /// <summary>
    /// Gets or sets the status shown.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the approval type shown, or empty for every type.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets whether the reader may see every request.
    /// </summary>
    [BindNever]
    public bool CanSeeAll { get; set; }

    /// <summary>
    /// Gets or sets the approval types to filter by.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> TypeOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the requests.
    /// </summary>
    [BindNever]
    public AdminListContext<ApprovalRequest> List { get; set; }
}

/// <summary>
/// The page of one request.
/// </summary>
public class ApprovalDecideViewModel
{
    /// <summary>
    /// Gets or sets the approver's note.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets the editor of the decision, built by the display drivers of this view model.
    /// </summary>
    [BindNever]
    public IShape Editor { get; set; }

    /// <summary>
    /// Gets or sets the request.
    /// </summary>
    [BindNever]
    public ApprovalRequest Request { get; set; }

    /// <summary>
    /// Gets or sets the request's type as people see it.
    /// </summary>
    [BindNever]
    public string TypeDisplayName { get; set; }

    /// <summary>
    /// Gets or sets what the type's handler shows for the subject.
    /// </summary>
    [BindNever]
    public string SubjectDisplay { get; set; }

    /// <summary>
    /// Gets or sets the subject's page from the type's handler.
    /// </summary>
    [BindNever]
    public string SubjectUrl { get; set; }

    /// <summary>
    /// Gets or sets the shape modules add fields to (display type <c>DetailAdmin</c>).
    /// </summary>
    [BindNever]
    public object Display { get; set; }

    /// <summary>
    /// Gets or sets whether the reader may decide.
    /// </summary>
    [BindNever]
    public ApprovalEligibility Eligibility { get; set; }

    /// <summary>
    /// Gets or sets whether the reader may withdraw it.
    /// </summary>
    [BindNever]
    public bool CanCancel { get; set; }

    /// <summary>
    /// Gets or sets when it was made, in the reader's time zone.
    /// </summary>
    [BindNever]
    public DateTimeOffset CreatedLocal { get; set; }

    /// <summary>
    /// Gets or sets when it is due, in the reader's time zone.
    /// </summary>
    [BindNever]
    public DateTimeOffset? DueLocal { get; set; }

    /// <summary>
    /// Gets or sets when each decision was made, in the reader's time zone.
    /// </summary>
    [BindNever]
    public IList<DateTimeOffset> DecisionTimes { get; set; } = [];
}
