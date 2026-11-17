using System.Security.Claims;
using CrestApps.OrchardCore.Approvals.Models;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Approvals.Services;

/// <summary>
/// One way to name approvers, which a policy's stages use by <see cref="Name"/> (<see cref="ApproverRule.Kind"/>),
/// such as a permission, a role, named users, the subject's manager, or a module's own (a ticket's assignee team, a
/// project lead). Register it with <c>services.AddApproverRuleProvider&lt;TProvider&gt;()</c>; its value's editor in
/// the policy editor is the <c>ApproverRuleValue_Edit</c> shape, with the alternate <c>ApproverRuleValue_Edit__{Name}</c>.
/// </summary>
public interface IApproverRuleProvider
{
    /// <summary>
    /// Gets the stored name; it never changes.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets what people see, such as "Anyone in the role".
    /// </summary>
    LocalizationSource DisplayName { get; }

    /// <summary>
    /// Gets a value indicating whether a rule needs a value, such as a role name.
    /// </summary>
    bool NeedsValue { get; }

    /// <summary>
    /// Gets a value indicating whether who the rule names depends on each request (the subject's manager), so a
    /// policy's approvers can't be counted ahead.
    /// </summary>
    bool DependsOnRequest { get; }

    /// <summary>
    /// Returns whether a person is named by a rule for a request. Deciding asks this, never the notice list.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="request">The request.</param>
    /// <param name="user">The person, signed in.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when the rule names them.</returns>
    Task<bool> IsNamedAsync(ApproverRule rule, ApprovalRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the user ids a rule names for a request, for notices, the "Waiting for me" list and the approver count.
    /// </summary>
    /// <param name="rule">The rule.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The user ids.</returns>
    Task<IReadOnlyList<string>> GetUserIdsAsync(ApproverRule rule, ApprovalRequest request, CancellationToken cancellationToken = default);
}
