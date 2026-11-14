using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Approvals.Core.Services;

/// <summary>
/// The default <see cref="IApprovalService"/>.
/// </summary>
public sealed class ApprovalService : IApprovalService
{
    private readonly IApprovalRequestManager _requests;
    private readonly INamedCatalogManager<ApprovalPolicy> _policies;
    private readonly IApproverDirectory _directory;
    private readonly IEnumerable<IApprovalSubjectHandler> _handlers;
    private readonly IEnumerable<IApprovalObserver> _observers;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalService"/> class.
    /// </summary>
    /// <param name="requests">The requests.</param>
    /// <param name="policies">The policies.</param>
    /// <param name="directory">Who the approver rules name.</param>
    /// <param name="handlers">The handlers of each subject kind.</param>
    /// <param name="observers">The observers of changes.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ApprovalService(
        IApprovalRequestManager requests,
        INamedCatalogManager<ApprovalPolicy> policies,
        IApproverDirectory directory,
        IEnumerable<IApprovalSubjectHandler> handlers,
        IEnumerable<IApprovalObserver> observers,
        IClock clock,
        IStringLocalizer<ApprovalService> stringLocalizer)
    {
        _requests = requests;
        _policies = policies;
        _directory = directory;
        _handlers = handlers;
        _observers = observers;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task<ApprovalResult> RequestAsync(ApprovalSubmission submission, ClaimsPrincipal requester, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        var errors = new List<string>();
        var policy = string.IsNullOrWhiteSpace(submission.Policy)
            ? null
            : await _policies.FindByIdAsync(submission.Policy.Trim(), cancellationToken) ?? await _policies.FindByNameAsync(submission.Policy.Trim(), cancellationToken);

        if (policy is null)
        {
            errors.Add(S["Choose the approval policy."]);
        }

        if (string.IsNullOrWhiteSpace(submission.SubjectKind) || string.IsNullOrWhiteSpace(submission.SubjectId))
        {
            errors.Add(S["Say what the approval is about."]);
        }

        if (string.IsNullOrWhiteSpace(submission.Title))
        {
            errors.Add(S["Say what is asked."]);
        }

        if (errors.Count > 0)
        {
            return new ApprovalResult { Errors = errors };
        }

        // Asking again while a request is pending gives that request.
        if (await _requests.FindPendingAsync(submission.SubjectKind, submission.SubjectId, policy.ItemId, cancellationToken) is { } pending)
        {
            return new ApprovalResult { Request = pending };
        }

        var request = await _requests.NewAsync(cancellationToken: cancellationToken);
        var (name, id) = Actor(requester);

        request.SubjectKind = submission.SubjectKind.Trim();
        request.SubjectId = submission.SubjectId.Trim();
        request.AccessScope = string.IsNullOrWhiteSpace(submission.AccessScope) ? null : submission.AccessScope.Trim();
        request.SubjectUrl = string.IsNullOrWhiteSpace(submission.SubjectUrl) ? null : submission.SubjectUrl.Trim();
        request.SubjectUserIds = (submission.SubjectUserIds ?? []).Where(entry => !string.IsNullOrEmpty(entry)).Distinct(StringComparer.Ordinal).ToList();
        request.SubjectStaffMemberId = string.IsNullOrWhiteSpace(submission.SubjectStaffMemberId) ? null : submission.SubjectStaffMemberId.Trim();
        request.Title = submission.Title.Trim();
        request.Amount = submission.Amount;
        request.Currency = submission.Currency;
        request.PolicyId = policy.ItemId;
        request.PolicyTitle = string.IsNullOrWhiteSpace(policy.Title) ? policy.Name : policy.Title;
        request.RequiredApprovals = Math.Max(1, policy.RequiredApprovals);
        request.RequesterUserId = id;
        request.RequesterName = name;
        request.Status = ApprovalStatus.Pending;
        request.CreatedUtc = _clock.UtcNow;
        request.DueUtc = policy.DueAfterHours is { } hours ? _clock.UtcNow.AddHours(hours) : null;

        await _requests.CreateAsync(request, cancellationToken);
        await NotifyAsync(request, ApprovalChangeKind.Requested, null, name, id, cancellationToken);

        return new ApprovalResult { Request = request };
    }

    /// <inheritdoc />
    public async Task<ApprovalEligibility> GetEligibilityAsync(ApprovalRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Status != ApprovalStatus.Pending || request.IsHistorical)
        {
            return new ApprovalEligibility(false, S["The request is already {0}.", request.Status.ToString()]);
        }

        var (_, userId) = Actor(user);

        if (userId is null)
        {
            return new ApprovalEligibility(false, S["Only a signed-in person can decide."]);
        }

        if (request.Decisions.Any(decision => decision.UserId == userId))
        {
            return new ApprovalEligibility(false, S["You already decided this request."]);
        }

        var policy = await _policies.FindByIdAsync(request.PolicyId, cancellationToken);

        if (policy is null)
        {
            return new ApprovalEligibility(false, S["The request's approval policy no longer exists."]);
        }

        if (!policy.AllowRequesterToApprove && userId == request.RequesterUserId)
        {
            return new ApprovalEligibility(false, S["You asked for this approval, so you can't decide it."]);
        }

        if (!policy.AllowSubjectToDecide && request.SubjectUserIds.Contains(userId, StringComparer.Ordinal))
        {
            return new ApprovalEligibility(false, S["The request is about you, so you can't decide it."]);
        }

        foreach (var rule in policy.Approvers)
        {
            if (await _directory.IsNamedAsync(rule, request, user, cancellationToken))
            {
                return ApprovalEligibility.Allowed;
            }
        }

        return new ApprovalEligibility(false, S["You are not an approver of this request."]);
    }

    /// <inheritdoc />
    public async Task<ApprovalResult> DecideAsync(ApprovalRequest request, ClaimsPrincipal user, ApprovalDecisionKind kind, string note, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var eligibility = await GetEligibilityAsync(request, user, cancellationToken);

        if (!eligibility.CanDecide)
        {
            return new ApprovalResult { Request = request, Errors = [eligibility.Reason] };
        }

        if (kind == ApprovalDecisionKind.Reject && string.IsNullOrWhiteSpace(note))
        {
            return new ApprovalResult { Request = request, Errors = [S["Say why you reject it."]] };
        }

        var (name, id) = Actor(user);
        var decision = new ApprovalDecision
        {
            UserId = id,
            UserName = name,
            Kind = kind,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            DecidedUtc = _clock.UtcNow,
        };

        request.Decisions.Add(decision);

        var change = ApprovalChangeKind.PartlyApproved;

        if (kind == ApprovalDecisionKind.Reject)
        {
            change = ApprovalChangeKind.Rejected;
            request.Status = ApprovalStatus.Rejected;
            request.ClosedUtc = _clock.UtcNow;
        }
        else if (request.ApprovalCount >= request.RequiredApprovals)
        {
            change = ApprovalChangeKind.Approved;
            request.Status = ApprovalStatus.Approved;
            request.ClosedUtc = _clock.UtcNow;
        }

        await _requests.UpdateAsync(request, cancellationToken: cancellationToken);

        if (request.Status != ApprovalStatus.Pending)
        {
            await CloseSubjectAsync(request, cancellationToken);
        }

        await NotifyAsync(request, change, decision, name, id, cancellationToken);

        return new ApprovalResult { Request = request };
    }

    /// <inheritdoc />
    public async Task<bool> CancelAsync(ApprovalRequest request, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.IsHistorical || !ApprovalStatuses.Transitions.CanMove(request.Status, ApprovalStatus.Canceled))
        {
            return false;
        }

        request.Status = ApprovalStatus.Canceled;
        request.CancelReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        request.ClosedUtc = _clock.UtcNow;

        await _requests.UpdateAsync(request, cancellationToken: cancellationToken);
        await CloseSubjectAsync(request, cancellationToken);
        await NotifyAsync(request, ApprovalChangeKind.Canceled, null, null, null, cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetApproverUserIdsAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var policy = await _policies.FindByIdAsync(request.PolicyId, cancellationToken);

        if (policy is null)
        {
            return [];
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rule in policy.Approvers)
        {
            ids.UnionWith(await _directory.GetUserIdsAsync(rule, request, cancellationToken));
        }

        if (!policy.AllowRequesterToApprove && request.RequesterUserId is not null)
        {
            ids.Remove(request.RequesterUserId);
        }

        if (!policy.AllowSubjectToDecide)
        {
            ids.ExceptWith(request.SubjectUserIds);
        }

        ids.ExceptWith(request.Decisions.Select(decision => decision.UserId).Where(entry => entry is not null));

        return ids.Order(StringComparer.Ordinal).ToList();
    }

    private async Task CloseSubjectAsync(ApprovalRequest request, CancellationToken cancellationToken)
    {
        foreach (var handler in _handlers.Where(handler => string.Equals(handler.SubjectKind, request.SubjectKind, StringComparison.Ordinal)))
        {
            await handler.ClosedAsync(request, cancellationToken);
        }
    }

    private async Task NotifyAsync(ApprovalRequest request, ApprovalChangeKind kind, ApprovalDecision decision, string actor, string actorId, CancellationToken cancellationToken)
    {
        var change = new ApprovalChange
        {
            Request = request,
            Kind = kind,
            Decision = decision,
            Actor = actor,
            ActorId = actorId,
        };

        foreach (var observer in _observers)
        {
            await observer.OnChangedAsync(change, cancellationToken);
        }
    }

    private static (string Name, string Id) Actor(ClaimsPrincipal user)
        => user?.Identity?.IsAuthenticated == true
            ? (user.Identity.Name, user.FindFirstValue(ClaimTypes.NameIdentifier))
            : (null, null);
}
