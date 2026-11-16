using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Approvals.Models;

/// <summary>
/// The record of a decision someone asked for about a subject of an approval type (such as a pay run, a ticket or a
/// time sheet), with the stages of the policy that say who may decide it, and the decisions made. A type keeps its own
/// data on the request with <c>Put</c> and reads it with <c>TryGet</c>.
/// </summary>
public sealed class ApprovalRequest : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the approval type's name (<c>ApprovalTypeOptions</c>), the kind of the subject, such as
    /// <c>PayRun</c>.
    /// </summary>
    public string SubjectKind { get; set; }

    /// <summary>
    /// Gets or sets the id of the subject.
    /// </summary>
    public string SubjectId { get; set; }

    /// <summary>
    /// Gets or sets the access scope within the subject kind, such as an employer; the <c>ISourceAccess</c> of the
    /// subject kind decides who sees the requests of each scope.
    /// </summary>
    public string AccessScope { get; set; }

    /// <summary>
    /// Gets or sets the page of the subject, application-relative (<c>~/...</c>), for the approver's notice.
    /// </summary>
    public string SubjectUrl { get; set; }

    /// <summary>
    /// Gets or sets the user ids of the people the subject is about, such as the employee of a pay change: none of
    /// them may decide unless the policy allows it.
    /// </summary>
    public List<string> SubjectUserIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the staff member the subject is about, for a policy whose approver is the manager.
    /// </summary>
    public string SubjectStaffMemberId { get; set; }

    /// <summary>
    /// Gets or sets what is asked, such as "Pay run 42, November 1 to 15".
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
    /// Gets or sets the policy's id.
    /// </summary>
    public string PolicyId { get; set; }

    /// <summary>
    /// Gets or sets the policy's title when the request was made.
    /// </summary>
    public string PolicyTitle { get; set; }

    /// <summary>
    /// Gets or sets how many approvals a request made before stages needs, copied from the policy when it was made.
    /// </summary>
    public int RequiredApprovals { get; set; } = 1;

    /// <summary>
    /// Gets or sets the stages, copied from the policy when the request was made, so a later change of the policy
    /// never changes who decides a request already asked. Empty for a request made before stages: it has one stage,
    /// <see cref="Approvers"/> and <see cref="RequiredApprovals"/>.
    /// </summary>
    public List<ApprovalStage> Stages { get; set; } = [];

    /// <summary>
    /// Gets or sets the index of the stage being decided, from 0.
    /// </summary>
    public int CurrentStage { get; set; }

    /// <summary>
    /// Gets or sets the requester's user id, or <see langword="null"/> when a workflow asked.
    /// </summary>
    public string RequesterUserId { get; set; }

    /// <summary>
    /// Gets or sets the requester's name.
    /// </summary>
    public string RequesterName { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public ApprovalStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the decisions made.
    /// </summary>
    public List<ApprovalDecision> Decisions { get; set; } = [];

    /// <summary>
    /// Gets or sets why it was cancelled.
    /// </summary>
    public string CancelReason { get; set; }

    /// <summary>
    /// Gets or sets when it was made.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it is due, or <see langword="null"/>.
    /// </summary>
    public DateTime? DueUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was decided or cancelled.
    /// </summary>
    public DateTime? ClosedUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether it was imported as history.
    /// </summary>
    public bool IsHistorical { get; set; }

    /// <summary>
    /// Gets or sets when it last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets who may decide a request made before stages, copied from the policy when it was made.
    /// </summary>
    public List<ApproverRule> Approvers { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the requester may approve, copied from the policy when the request was made.
    /// </summary>
    public bool AllowRequesterToApprove { get; set; }

    /// <summary>
    /// Gets or sets whether a person may decide a request about themselves, copied from the policy when the request was
    /// made.
    /// </summary>
    public bool AllowSubjectToDecide { get; set; }

    /// <summary>
    /// Gets or sets the version of the subject the request is about, such as a fingerprint of its totals, from the
    /// caller. A request about another version replaces it, and the subject handler can refuse a stale approval.
    /// </summary>
    public string SubjectVersion { get; set; }

    /// <summary>
    /// Gets the number of approvals made, in every stage.
    /// </summary>
    public int ApprovalCount
        => Decisions.Count(decision => decision.Kind == ApprovalDecisionKind.Approve);

    /// <summary>
    /// Gets the number of approvals made in the current stage.
    /// </summary>
    public int StageApprovalCount
        => Decisions.Count(decision => decision.Kind == ApprovalDecisionKind.Approve && decision.Stage == CurrentStage);

    /// <summary>
    /// Returns the request's stages: <see cref="Stages"/>, or the one stage of a request made before stages.
    /// </summary>
    /// <returns>The stages.</returns>
    public IReadOnlyList<ApprovalStage> GetStages()
        => Stages is { Count: > 0 }
            ? Stages
            : [new ApprovalStage { Approvers = Approvers ?? [], RequiredApprovals = Math.Max(1, RequiredApprovals) }];

    /// <summary>
    /// Returns the stage being decided.
    /// </summary>
    /// <returns>The stage.</returns>
    public ApprovalStage GetCurrentStage()
    {
        var stages = GetStages();

        return stages[Math.Clamp(CurrentStage, 0, stages.Count - 1)];
    }
}
