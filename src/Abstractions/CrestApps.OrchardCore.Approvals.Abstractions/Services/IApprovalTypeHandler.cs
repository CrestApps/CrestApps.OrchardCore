using System.Security.Claims;
using CrestApps.OrchardCore.Approvals.Models;

namespace CrestApps.OrchardCore.Approvals.Services;

/// <summary>
/// Fills, checks and follows the requests of one approval type. Derive from <see cref="ApprovalTypeHandlerBase"/> and
/// override the hooks the type needs. The closing hooks run inside the unit of work of the decision, so the subject
/// moves with it (a pay run becomes approved) or not at all.
/// </summary>
public interface IApprovalTypeHandler
{
    /// <summary>
    /// Called before a request is stored: fill what the caller left out (the title, the subject's page, the people the
    /// subject is about, its staff member), add approvers to the stages, store the type's data on the request with
    /// <c>Put</c>, or refuse it.
    /// </summary>
    /// <param name="context">The request being made.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task RequestingAsync(ApprovalRequestingContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called before a decision is recorded, once the person is known to be an approver: refuse it, such as when the
    /// subject changed since the request was made.
    /// </summary>
    /// <param name="context">The decision being made.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task DecidingAsync(ApprovalDecidingContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether a person a stage names may act on this request's record, beyond the type's source access
    /// (<c>ISourceAccess</c> for the type and the request's access scope), which Approvals checks first.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="user">The person.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when they may.</returns>
    Task<bool> CanAccessAsync(ApprovalRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after a stage that isn't the last is approved and the next one starts.
    /// </summary>
    /// <param name="request">The request, at its next stage.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task StageApprovedAsync(ApprovalRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the access scopes a policy of the type can be limited to, for a type whose
    /// <see cref="ApprovalType.Conditions"/> include <see cref="ApprovalConditions.Scope"/>, such as the employers.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The scopes.</returns>
    Task<IReadOnlyList<ApprovalScope>> GetScopesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns whether a policy of the type matches what is asked by the type's own conditions, after its type and
    /// standard conditions matched; the conditions are what the type's policy display driver stored on the policy.
    /// </summary>
    /// <param name="policy">The policy.</param>
    /// <param name="submission">What is asked.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="true"/> when it matches.</returns>
    Task<bool> MatchesAsync(ApprovalPolicy policy, ApprovalSubmission submission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after the last stage is approved.
    /// </summary>
    /// <param name="request">The approved request.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task ApprovedAsync(ApprovalRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after an approver rejects the request, at any stage.
    /// </summary>
    /// <param name="request">The rejected request.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task RejectedAsync(ApprovalRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called after a pending request is withdrawn or replaced.
    /// </summary>
    /// <param name="request">The withdrawn request.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>A task.</returns>
    Task CanceledAsync(ApprovalRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the page of the request's subject, application-relative (<c>~/...</c>), or <see langword="null"/>.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The page.</returns>
    Task<string> GetSubjectUrlAsync(ApprovalRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns what people see for the request's subject.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The text.</returns>
    Task<string> GetSubjectDisplayAsync(ApprovalRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// An <see cref="IApprovalTypeHandler"/> whose hooks do nothing: override the ones a type needs.
/// </summary>
public abstract class ApprovalTypeHandlerBase : IApprovalTypeHandler
{
    /// <inheritdoc />
    public virtual Task RequestingAsync(ApprovalRequestingContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task DecidingAsync(ApprovalDecidingContext context, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task<bool> CanAccessAsync(ApprovalRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    /// <inheritdoc />
    public virtual Task StageApprovedAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task<IReadOnlyList<ApprovalScope>> GetScopesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ApprovalScope>>([]);

    /// <inheritdoc />
    public virtual Task<bool> MatchesAsync(ApprovalPolicy policy, ApprovalSubmission submission, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    /// <inheritdoc />
    public virtual Task ApprovedAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task RejectedAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task CanceledAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    /// <inheritdoc />
    public virtual Task<string> GetSubjectUrlAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(request?.SubjectUrl);

    /// <inheritdoc />
    public virtual Task<string> GetSubjectDisplayAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(request?.Title);
}

/// <summary>
/// A request about to be stored, for <see cref="IApprovalTypeHandler.RequestingAsync"/>.
/// </summary>
public sealed class ApprovalRequestingContext
{
    /// <summary>
    /// Gets what was asked.
    /// </summary>
    public required ApprovalSubmission Submission { get; init; }

    /// <summary>
    /// Gets the request being made, with its policy's stages; changes to it are stored.
    /// </summary>
    public required ApprovalRequest Request { get; init; }

    /// <summary>
    /// Gets who asks, or <see langword="null"/> for a workflow.
    /// </summary>
    public ClaimsPrincipal Requester { get; init; }

    /// <summary>
    /// Gets why the request is refused.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Refuses the request.
    /// </summary>
    /// <param name="reason">Why, for the person who asked.</param>
    public void Refuse(string reason)
        => Errors.Add(reason);

    /// <summary>
    /// Lets the people a rule names decide every stage of this request too, such as the approvers of one employer.
    /// </summary>
    /// <param name="rule">The rule.</param>
    public void AddApprover(ApproverRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        foreach (var stage in Request.Stages)
        {
            if (!stage.Approvers.Any(existing => string.Equals(existing.Kind, rule.Kind, StringComparison.OrdinalIgnoreCase) && string.Equals(existing.Value, rule.Value, StringComparison.Ordinal)))
            {
                stage.Approvers.Add(rule.Clone());
            }
        }
    }
}

/// <summary>
/// A decision about to be recorded, for <see cref="IApprovalTypeHandler.DecidingAsync"/>.
/// </summary>
public sealed class ApprovalDecidingContext
{
    /// <summary>
    /// Gets the request.
    /// </summary>
    public required ApprovalRequest Request { get; init; }

    /// <summary>
    /// Gets the approver.
    /// </summary>
    public required ClaimsPrincipal User { get; init; }

    /// <summary>
    /// Gets the decision.
    /// </summary>
    public ApprovalDecisionKind Kind { get; init; }

    /// <summary>
    /// Gets the approver's note.
    /// </summary>
    public string Note { get; init; }

    /// <summary>
    /// Gets why the decision is refused.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Refuses the decision.
    /// </summary>
    /// <param name="reason">Why, for the approver.</param>
    public void Refuse(string reason)
        => Errors.Add(reason);
}
