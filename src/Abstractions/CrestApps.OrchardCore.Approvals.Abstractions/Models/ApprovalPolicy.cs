using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Approvals.Models;

/// <summary>
/// Who may decide approvals of some types, in what stages, and how many of them must approve each stage. A request
/// names its policy, or gets the first enabled one whose types and conditions match (<c>IApprovalPolicyResolver</c>).
/// A request follows the policy as it was when it was made; the two built-in rules (the requester can't approve,
/// nobody decides a request about their own record) apply at every stage and can each be turned off, such as for a
/// one-person business.
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
    /// Gets or sets the approval type the policy serves (<c>ApprovalTypeOptions</c>), chosen when it is added and fixed
    /// afterwards; the type decides which conditions and fields the policy editor shows. Empty for a general policy, one
    /// saved before policies had one type, which serves the types of <see cref="Types"/>, or every type.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the approval types a policy saved before policies had one type serves; empty serves any type. A
    /// policy with one is read as that <see cref="Type"/>.
    /// </summary>
    public List<string> Types { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the policy is chosen for requests that name no policy. A request that
    /// names it still uses it.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets where the policy comes when one is chosen for a request that names none; lower first.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets the least amount a request must have for the policy to be chosen, or <see langword="null"/>.
    /// </summary>
    public decimal? MinimumAmount { get; set; }

    /// <summary>
    /// Gets or sets the most amount a request may have for the policy to be chosen, or <see langword="null"/>.
    /// </summary>
    public decimal? MaximumAmount { get; set; }

    /// <summary>
    /// Gets or sets the currency a request's amount must be in for the policy to be chosen, or <see langword="null"/>
    /// for any.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the access scope a request must have for the policy to be chosen, such as one employer, or
    /// <see langword="null"/> for any.
    /// </summary>
    public string AccessScope { get; set; }

    /// <summary>
    /// Gets or sets the stages, decided one after the other. Empty means one stage made of <see cref="Approvers"/>
    /// and <see cref="RequiredApprovals"/>, as a policy saved before stages had.
    /// </summary>
    public List<ApprovalStage> Stages { get; set; } = [];

    /// <summary>
    /// Gets or sets the ways to be an approver of a policy without <see cref="Stages"/>; anyone any rule names may
    /// approve.
    /// </summary>
    public List<ApproverRule> Approvers { get; set; } = [];

    /// <summary>
    /// Gets or sets how many different approvers must approve a policy without <see cref="Stages"/>, 1 or more. One
    /// rejection rejects the request.
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
    /// Gets or sets a value indicating whether one person may decide more than one stage of a request, such as in a
    /// team too small to name different people for each stage.
    /// </summary>
    public bool AllowSamePersonAcrossStages { get; set; }

    /// <summary>
    /// Gets or sets how many hours after it is made a request is due, or <see langword="null"/>.
    /// </summary>
    public int? DueAfterHours { get; set; }

    /// <summary>
    /// Gets or sets when the policy last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Returns the stages a request under the policy goes through: <see cref="Stages"/>, or one stage made of
    /// <see cref="Approvers"/> and <see cref="RequiredApprovals"/>.
    /// </summary>
    /// <returns>Copies of the stages.</returns>
    public IReadOnlyList<ApprovalStage> GetStages()
        => Stages is { Count: > 0 }
            ? [.. Stages.Select(stage => stage.Clone())]
            : [new ApprovalStage { Approvers = [.. (Approvers ?? []).Select(rule => rule.Clone())], RequiredApprovals = RequiredApprovals }];

    /// <summary>
    /// Returns whether the policy serves an approval type.
    /// </summary>
    /// <param name="type">The type's name.</param>
    /// <returns><see langword="true"/> when it is the policy's type, or a general policy lists it or lists none.</returns>
    public bool Serves(string type)
        => !string.IsNullOrEmpty(Type)
            ? string.Equals(Type, type, StringComparison.OrdinalIgnoreCase)
            : Types is not { Count: > 0 } || Types.Contains(type, StringComparer.OrdinalIgnoreCase);
}
