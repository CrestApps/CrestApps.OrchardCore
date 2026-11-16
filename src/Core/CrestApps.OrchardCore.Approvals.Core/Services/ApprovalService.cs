using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Locking.Distributed;
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
    private readonly IApprovalPolicyResolver _resolver;
    private readonly IApprovalTypeCatalog _types;
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(60);

    private readonly IEnumerable<IApprovalObserver> _observers;
    private readonly IDistributedLock _locks;
    private readonly IApprovalCommitter _committer;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalService"/> class.
    /// </summary>
    /// <param name="requests">The requests.</param>
    /// <param name="policies">The policies.</param>
    /// <param name="directory">Who the approver rules name.</param>
    /// <param name="resolver">Chooses the policy of a request that names none.</param>
    /// <param name="types">The approval types and their handlers.</param>
    /// <param name="observers">The observers of changes.</param>
    /// <param name="locks">The locks, one per subject and per request.</param>
    /// <param name="committer">Commits while a lock is held.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ApprovalService(
        IApprovalRequestManager requests,
        INamedCatalogManager<ApprovalPolicy> policies,
        IApproverDirectory directory,
        IApprovalPolicyResolver resolver,
        IApprovalTypeCatalog types,
        IEnumerable<IApprovalObserver> observers,
        IDistributedLock locks,
        IApprovalCommitter committer,
        IClock clock,
        IStringLocalizer<ApprovalService> stringLocalizer)
    {
        _requests = requests;
        _policies = policies;
        _directory = directory;
        _resolver = resolver;
        _types = types;
        _observers = observers;
        _locks = locks;
        _committer = committer;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task<ApprovalResult> RequestAsync(ApprovalSubmission submission, ClaimsPrincipal requester, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(submission.SubjectKind) || string.IsNullOrWhiteSpace(submission.SubjectId))
        {
            errors.Add(S["Say what the approval is about."]);
        }

        var policy = await _resolver.ResolveAsync(submission, cancellationToken);

        if (policy is null)
        {
            errors.Add(string.IsNullOrWhiteSpace(submission.Policy)
                ? S["No enabled approval policy serves {0} with this amount, currency and scope. Add one under Approvals > Policies, or name the policy.", submission.SubjectKind?.Trim() ?? string.Empty]
                : S["The approval policy {0} doesn't exist.", submission.Policy.Trim()]);
        }

        if (errors.Count > 0)
        {
            return new ApprovalResult { Errors = errors };
        }

        // One request at a time per subject, from the read to the commit, so two at once never both create.
        var (locker, locked) = await _locks.TryAcquireLockAsync($"Approvals:Subject:{submission.SubjectKind.Trim()}:{submission.SubjectId.Trim()}", _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return new ApprovalResult { Errors = [S["Another request about this record is being made. Try again in a moment."]] };
        }

        await using (locker)
        {
            var version = string.IsNullOrWhiteSpace(submission.SubjectVersion) ? null : submission.SubjectVersion.Trim();

            // Asking again while a request is pending gives that request; asking about another version replaces it.
            if (await _requests.FindPendingAsync(submission.SubjectKind.Trim(), submission.SubjectId.Trim(), policy.ItemId, cancellationToken) is { } pending)
            {
                if (string.Equals(pending.SubjectVersion, version, StringComparison.Ordinal))
                {
                    return new ApprovalResult { Request = pending };
                }

                await CloseCanceledAsync(pending, S["Replaced: the record changed after approval was asked."], cancellationToken);
            }

            var result = await CreateRequestAsync(submission, policy, version, requester, cancellationToken);

            if (!result.Succeeded)
            {
                return result;
            }

            await _committer.CommitAsync(cancellationToken);

            return result;
        }
    }

    private async Task<ApprovalResult> CreateRequestAsync(ApprovalSubmission submission, ApprovalPolicy policy, string version, ClaimsPrincipal requester, CancellationToken cancellationToken)
    {
        var request = await _requests.NewAsync(cancellationToken: cancellationToken);
        var (name, id) = Actor(requester);

        request.SubjectKind = submission.SubjectKind.Trim();
        request.SubjectId = submission.SubjectId.Trim();
        request.AccessScope = string.IsNullOrWhiteSpace(submission.AccessScope) ? null : submission.AccessScope.Trim();
        request.SubjectUrl = string.IsNullOrWhiteSpace(submission.SubjectUrl) ? null : submission.SubjectUrl.Trim();
        request.SubjectUserIds = (submission.SubjectUserIds ?? []).Where(entry => !string.IsNullOrEmpty(entry)).Distinct(StringComparer.Ordinal).ToList();
        request.SubjectStaffMemberId = string.IsNullOrWhiteSpace(submission.SubjectStaffMemberId) ? null : submission.SubjectStaffMemberId.Trim();
        request.Title = submission.Title?.Trim();
        request.Amount = submission.Amount;
        request.Currency = submission.Currency;
        request.PolicyId = policy.ItemId;
        request.PolicyTitle = string.IsNullOrWhiteSpace(policy.Title) ? policy.Name : policy.Title;
        // The stages of the policy as they are now: a later change of the policy never changes this request.
        request.Stages = [.. policy.GetStages()];
        request.CurrentStage = 0;
        request.RequiredApprovals = Math.Max(1, request.Stages[0].RequiredApprovals);
        request.AllowRequesterToApprove = policy.AllowRequesterToApprove;
        request.AllowSubjectToDecide = policy.AllowSubjectToDecide;
        request.SubjectVersion = version;
        request.RequesterUserId = id;
        request.RequesterName = name;
        request.Status = ApprovalStatus.Pending;
        request.CreatedUtc = _clock.UtcNow;
        request.DueUtc = policy.DueAfterHours is { } hours ? _clock.UtcNow.AddHours(hours) : null;

        // The type's handler fills what the caller left out, adds approvers, or refuses.
        var context = new ApprovalRequestingContext { Submission = submission, Request = request, Requester = requester };

        await _types.GetHandler(request.SubjectKind).RequestingAsync(context, cancellationToken);

        if (context.Errors.Count == 0 && string.IsNullOrWhiteSpace(request.Title))
        {
            context.Refuse(S["Say what is asked."]);
        }

        if (context.Errors.Count > 0)
        {
            return new ApprovalResult { Errors = [.. context.Errors] };
        }

        request.Title = request.Title.Trim();

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

        // One person decides one stage at most: who approved the manager stage doesn't also approve finance.
        if (request.Decisions.Any(decision => decision.UserId == userId))
        {
            return new ApprovalEligibility(false, S["You already decided this request."]);
        }

        if (await RulesOfAsync(request, cancellationToken) is not { } rules)
        {
            return new ApprovalEligibility(false, S["The request's approval policy no longer exists."]);
        }

        if (!rules.AllowRequesterToApprove && userId == request.RequesterUserId)
        {
            return new ApprovalEligibility(false, S["You asked for this approval, so you can't decide it."]);
        }

        if (!rules.AllowSubjectToDecide && request.SubjectUserIds.Contains(userId, StringComparer.Ordinal))
        {
            return new ApprovalEligibility(false, S["The request is about you, so you can't decide it."]);
        }

        foreach (var rule in rules.Approvers)
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

        // One decision at a time per request, on a fresh read, so two approvers at once both count.
        var (locker, locked) = await _locks.TryAcquireLockAsync("Approvals:Request:" + request.ItemId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return new ApprovalResult { Request = request, Errors = [S["Someone else is deciding this request. Try again in a moment."]] };
        }

        await using (locker)
        {
            var current = await _requests.FindByIdAsync(request.ItemId, cancellationToken) ?? request;
            var result = await DecideLockedAsync(current, user, kind, note, cancellationToken);

            await _committer.CommitAsync(cancellationToken);
            CopyState(current, request);

            return result.Succeeded ? new ApprovalResult { Request = request } : new ApprovalResult { Request = request, Errors = result.Errors };
        }
    }

    private async Task<ApprovalResult> DecideLockedAsync(ApprovalRequest request, ClaimsPrincipal user, ApprovalDecisionKind kind, string note, CancellationToken cancellationToken)
    {
        var eligibility = await GetEligibilityAsync(request, user, cancellationToken);

        if (!eligibility.CanDecide)
        {
            return new ApprovalResult { Request = request, Errors = [eligibility.Reason] };
        }

        if (kind == ApprovalDecisionKind.Reject && string.IsNullOrWhiteSpace(note))
        {
            return new ApprovalResult { Request = request, Errors = [S["Say why you reject it."]] };
        }

        var handler = _types.GetHandler(request.SubjectKind);
        var deciding = new ApprovalDecidingContext { Request = request, User = user, Kind = kind, Note = note };

        await handler.DecidingAsync(deciding, cancellationToken);

        if (deciding.Errors.Count > 0)
        {
            return new ApprovalResult { Request = request, Errors = [.. deciding.Errors] };
        }

        var (name, id) = Actor(user);
        var decision = new ApprovalDecision
        {
            UserId = id,
            UserName = name,
            Kind = kind,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            Stage = request.CurrentStage,
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
        else if (request.StageApprovalCount >= Math.Max(1, (await StageOfAsync(request, cancellationToken)).RequiredApprovals))
        {
            if (request.CurrentStage < (await StagesOfAsync(request, cancellationToken)).Count - 1)
            {
                // The next stage starts: its approvers are told, and its approvals are counted from none.
                change = ApprovalChangeKind.StageApproved;
                request.CurrentStage++;
            }
            else
            {
                change = ApprovalChangeKind.Approved;
                request.Status = ApprovalStatus.Approved;
                request.ClosedUtc = _clock.UtcNow;
            }
        }

        await _requests.UpdateAsync(request, cancellationToken: cancellationToken);

        switch (change)
        {
            case ApprovalChangeKind.Approved:
                await handler.ApprovedAsync(request, cancellationToken);
                break;
            case ApprovalChangeKind.Rejected:
                await handler.RejectedAsync(request, cancellationToken);
                break;
            case ApprovalChangeKind.StageApproved:
                await handler.StageApprovedAsync(request, cancellationToken);
                break;
        }

        await NotifyAsync(request, change, decision, name, id, cancellationToken);

        return new ApprovalResult { Request = request };
    }

    /// <inheritdoc />
    public async Task<bool> CancelAsync(ApprovalRequest request, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (locker, locked) = await _locks.TryAcquireLockAsync("Approvals:Request:" + request.ItemId, _lockTimeout, _lockExpiration);

        if (!locked)
        {
            return false;
        }

        await using (locker)
        {
            var current = await _requests.FindByIdAsync(request.ItemId, cancellationToken) ?? request;
            var canceled = await CloseCanceledAsync(current, reason, cancellationToken);

            await _committer.CommitAsync(cancellationToken);
            CopyState(current, request);

            return canceled;
        }
    }

    private async Task<bool> CloseCanceledAsync(ApprovalRequest request, string reason, CancellationToken cancellationToken)
    {
        if (request.IsHistorical || !ApprovalStatuses.Transitions.CanMove(request.Status, ApprovalStatus.Canceled))
        {
            return false;
        }

        request.Status = ApprovalStatus.Canceled;
        request.CancelReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        request.ClosedUtc = _clock.UtcNow;

        await _requests.UpdateAsync(request, cancellationToken: cancellationToken);
        await _types.GetHandler(request.SubjectKind).CanceledAsync(request, cancellationToken);
        await NotifyAsync(request, ApprovalChangeKind.Canceled, null, null, null, cancellationToken);

        return true;
    }

    // The stages of a request: its own copy, or its policy's for a request made before rules were copied.
    private async Task<IReadOnlyList<ApprovalStage>> StagesOfAsync(ApprovalRequest request, CancellationToken cancellationToken)
    {
        if (request.Stages is { Count: > 0 } || request.Approvers is { Count: > 0 })
        {
            return request.GetStages();
        }

        return await _policies.FindByIdAsync(request.PolicyId, cancellationToken) is { } policy ? policy.GetStages() : [];
    }

    private async Task<ApprovalStage> StageOfAsync(ApprovalRequest request, CancellationToken cancellationToken)
    {
        var stages = await StagesOfAsync(request, cancellationToken);

        return stages.Count == 0 ? new ApprovalStage() : stages[Math.Clamp(request.CurrentStage, 0, stages.Count - 1)];
    }

    // The rules the current stage is decided by, and the two built-in rules, which hold at every stage.
    private async Task<(IReadOnlyList<ApproverRule> Approvers, bool AllowRequesterToApprove, bool AllowSubjectToDecide)?> RulesOfAsync(ApprovalRequest request, CancellationToken cancellationToken)
    {
        if (request.Stages is { Count: > 0 } || request.Approvers is { Count: > 0 })
        {
            return (request.GetCurrentStage().Approvers, request.AllowRequesterToApprove, request.AllowSubjectToDecide);
        }

        var policy = await _policies.FindByIdAsync(request.PolicyId, cancellationToken);

        if (policy is null)
        {
            return null;
        }

        var stages = policy.GetStages();

        return (stages[Math.Clamp(request.CurrentStage, 0, stages.Count - 1)].Approvers, policy.AllowRequesterToApprove, policy.AllowSubjectToDecide);
    }

    private static void CopyState(ApprovalRequest from, ApprovalRequest to)
    {
        if (ReferenceEquals(from, to))
        {
            return;
        }

        to.Status = from.Status;
        to.Decisions = from.Decisions;
        to.CurrentStage = from.CurrentStage;
        to.ClosedUtc = from.ClosedUtc;
        to.CancelReason = from.CancelReason;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetApproverUserIdsAsync(ApprovalRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await RulesOfAsync(request, cancellationToken) is not { } rules)
        {
            return [];
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rule in rules.Approvers)
        {
            ids.UnionWith(await _directory.GetUserIdsAsync(rule, request, cancellationToken));
        }

        if (!rules.AllowRequesterToApprove && request.RequesterUserId is not null)
        {
            ids.Remove(request.RequesterUserId);
        }

        if (!rules.AllowSubjectToDecide)
        {
            ids.ExceptWith(request.SubjectUserIds);
        }

        ids.ExceptWith(request.Decisions.Select(decision => decision.UserId).Where(entry => entry is not null));

        return ids.Order(StringComparer.Ordinal).ToList();
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
