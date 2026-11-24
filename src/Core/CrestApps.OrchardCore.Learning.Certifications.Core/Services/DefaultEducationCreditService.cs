using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Ledgers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Learning.Certifications.Core.Services;

/// <summary>
/// Keeps education credit types and external learning, and posts credits to the education credit ledger once per
/// source. After credits are posted, the certification paths that ask for credits of the type are checked again.
/// </summary>
public sealed class DefaultEducationCreditService : IEducationCreditService
{
    private readonly EducationCreditTypeStore _types;
    private readonly ExternalLearningSubmissionStore _submissions;
    private readonly IQuantityLedger<EducationCreditLedger> _ledger;
    private readonly IServiceProvider _services;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public DefaultEducationCreditService(
        EducationCreditTypeStore types,
        ExternalLearningSubmissionStore submissions,
        IQuantityLedger<EducationCreditLedger> ledger,
        IServiceProvider services,
        IDistributedLock distributedLock,
        ISession session,
        IClock clock,
        ILogger<DefaultEducationCreditService> logger,
        IStringLocalizer<DefaultEducationCreditService> stringLocalizer)
    {
        _types = types;
        _submissions = submissions;
        _ledger = ledger;
        _services = services;
        _distributedLock = distributedLock;
        _session = session;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<EducationCreditType>> ListTypesAsync(CancellationToken cancellationToken = default)
        => _types.ListAllAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<EducationCreditType> FindTypeAsync(string typeId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(typeId) ? null : await _types.FindByIdAsync(typeId, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> SaveTypeAsync(EducationCreditType type, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(type);

        var errors = new List<string>();

        type.Name = type.Name?.Trim();
        type.Unit = type.Unit?.Trim();

        if (string.IsNullOrEmpty(type.Name))
        {
            errors.Add(S["The name is required."]);
        }
        else if ((await _types.ListAllAsync(cancellationToken)).Any(other => other.ItemId != type.ItemId && string.Equals(other.Name, type.Name, StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add(S["Another credit type is named '{0}'.", type.Name]);
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        if (string.IsNullOrEmpty(type.ItemId) || await _types.FindByIdAsync(type.ItemId, cancellationToken) is null)
        {
            type.CreatedUtc = _clock.UtcNow;
            await _types.CreateAsync(type, cancellationToken);
        }
        else
        {
            await _types.UpdateAsync(type, cancellationToken);
        }

        return errors;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteTypeAsync(string typeId, CancellationToken cancellationToken = default)
    {
        var type = await FindTypeAsync(typeId, cancellationToken);

        return type is not null && await _types.DeleteAsync(type, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<QuantityLedgerPostResult> AwardAsync(EducationCreditAwardRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.CreditTypeId) || request.Amount <= 0 || request.Amount > CertificationsConstants.MaxCreditsPerAward)
        {
            return null;
        }

        // A credit type that was deleted receives nothing.
        if (await FindTypeAsync(request.CreditTypeId, cancellationToken) is null)
        {
            return null;
        }

        var result = await _ledger.PostAsync(new QuantityLedgerEntry
        {
            AccountId = request.UserId,
            Bucket = request.CreditTypeId,
            Kind = request.Source,
            Amount = request.Amount,
            EffectiveDate = request.EarnedOn,
            ExpiresOn = request.ExpiresOn,
            SourceRef = request.SourceId,
            // One award per source, credit type and person: a webinar named as the source credits each attendee once.
            IdempotencyKey = $"{request.Source}:{request.SourceId}:{request.CreditTypeId}:{request.UserId}",
            Actor = request.Actor,
            Note = request.Note,
        }, cancellationToken);

        if (result.Created)
        {
            await _session.SaveChangesAsync(cancellationToken);

            foreach (var observer in _services.GetServices<IEducationCreditObserver>())
            {
                await observer.AwardedAsync(result.Entry, cancellationToken);
            }

            await EvaluatePathsAsync(request.CreditTypeId, request.UserId, cancellationToken);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, decimal>> GetBalancesAsync(string userId, DateOnly asOf, CancellationToken cancellationToken = default)
    {
        var balances = new Dictionary<string, decimal>(StringComparer.Ordinal);

        foreach (var bucket in (await _ledger.ListForAccountAsync(userId, cancellationToken)).Select(entry => entry.Bucket).Distinct(StringComparer.Ordinal))
        {
            balances[bucket] = await _ledger.GetBalanceAsync(userId, bucket, asOf, cancellationToken);
        }

        return balances;
    }

    /// <inheritdoc/>
    public Task<decimal> SumAsync(string userId, string typeId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
        => _ledger.SumAsync(userId, typeId, from, to, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<QuantityLedgerEntry>> ListReversibleAsync(string userId, string sourceRef, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(sourceRef))
        {
            return [];
        }

        var entries = await _ledger.ListForAccountAsync(userId, cancellationToken);
        var reversed = entries.Where(entry => entry.Kind == CertificationsConstants.CreditSources.Reversal).Select(entry => entry.IdempotencyKey).ToHashSet(StringComparer.Ordinal);

        return entries
            .Where(entry => entry.SourceRef == sourceRef && entry.Amount > 0 && entry.Kind != CertificationsConstants.CreditSources.Reversal && !reversed.Contains(ReversalKey(entry)))
            .ToArray();
    }

    /// <inheritdoc/>
    public async Task<int> ReverseAsync(string userId, string sourceRef, string actor, string reason, CancellationToken cancellationToken = default)
    {
        var count = 0;

        foreach (var entry in await ListReversibleAsync(userId, sourceRef, cancellationToken))
        {
            // On the same days as the entry it takes back, so it leaves the sums of every period as if the credits had
            // never been posted.
            var result = await _ledger.PostAsync(new QuantityLedgerEntry
            {
                AccountId = entry.AccountId,
                Bucket = entry.Bucket,
                Kind = CertificationsConstants.CreditSources.Reversal,
                Amount = -entry.Amount,
                EffectiveDate = entry.EffectiveDate,
                ExpiresOn = entry.ExpiresOn,
                SourceRef = sourceRef,
                IdempotencyKey = ReversalKey(entry),
                Actor = actor,
                Note = reason,
            }, cancellationToken);

            if (result.Created)
            {
                count++;
            }
        }

        if (count > 0)
        {
            await _session.SaveChangesAsync(cancellationToken);
        }

        return count;
    }

    private static string ReversalKey(QuantityLedgerEntry entry) => $"reversal:{entry.ItemId}";

    /// <inheritdoc/>
    public async Task<IReadOnlyList<QuantityLedgerEntry>> ListEntriesAsync(string userId, CancellationToken cancellationToken = default)
        => (await _ledger.ListForAccountAsync(userId, cancellationToken)).OrderByDescending(entry => entry.EffectiveDate).ToArray();

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> SubmitExternalAsync(ExternalLearningSubmission submission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        var errors = new List<string>();
        var type = await FindTypeAsync(submission.CreditTypeId, cancellationToken);

        submission.Title = submission.Title?.Trim();
        submission.Provider = submission.Provider?.Trim();

        if (string.IsNullOrEmpty(submission.UserId))
        {
            errors.Add(S["Say who did the learning."]);
        }

        if (string.IsNullOrEmpty(submission.Title))
        {
            errors.Add(S["Say what the learning was."]);
        }

        if (type is null || !type.AllowExternalLearning)
        {
            errors.Add(S["Choose a credit type that takes external learning."]);
        }

        if (submission.Amount <= 0)
        {
            errors.Add(S["The credits must be more than zero."]);
        }
        else if (submission.Amount > CertificationsConstants.MaxCreditsPerAward)
        {
            errors.Add(S["The credits can't be more than {0}.", CertificationsConstants.MaxCreditsPerAward]);
        }

        if (submission.CompletedOn > DateOnly.FromDateTime(_clock.UtcNow.AddDays(1)))
        {
            errors.Add(S["The learning can't be completed in the future."]);
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        submission.State = ExternalLearningState.Submitted;
        submission.SubmittedUtc = _clock.UtcNow;
        await _submissions.CreateAsync(submission, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        foreach (var observer in _services.GetServices<IEducationCreditObserver>())
        {
            await observer.SubmittedAsync(submission, cancellationToken);
        }

        return errors;
    }

    /// <inheritdoc/>
    public Task<ExternalLearningReviewResult> AcceptExternalAsync(string submissionId, string reviewerUserId, string actor, string note, CancellationToken cancellationToken = default)
        => ReviewAsync(submissionId, reviewerUserId, accept: true, actor, note, cancellationToken);

    /// <inheritdoc/>
    public Task<ExternalLearningReviewResult> RejectExternalAsync(string submissionId, string reviewerUserId, string actor, string note, CancellationToken cancellationToken = default)
        => ReviewAsync(submissionId, reviewerUserId, accept: false, actor, note, cancellationToken);

    // Accepting and rejecting run under a lock per submission on a fresh read, so two reviewers never both post, and a
    // rejection never lands on a submission whose credits were posted.
    private async Task<ExternalLearningReviewResult> ReviewAsync(string submissionId, string reviewerUserId, bool accept, string actor, string note, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(submissionId))
        {
            return new ExternalLearningReviewResult(ExternalLearningReviewOutcome.NotFound, null);
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync($"Learning:ExternalLearning:{submissionId}", TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2));

        if (!locked)
        {
            return new ExternalLearningReviewResult(ExternalLearningReviewOutcome.Busy, await FindExternalAsync(submissionId, cancellationToken));
        }

        ExternalLearningSubmission submission;

        await using (locker)
        {
            submission = await _submissions.FindFreshByIdAsync(submissionId, cancellationToken);

            if (submission is null)
            {
                return new ExternalLearningReviewResult(ExternalLearningReviewOutcome.NotFound, null);
            }

            if (string.IsNullOrEmpty(reviewerUserId) || string.Equals(reviewerUserId, submission.UserId, StringComparison.Ordinal))
            {
                return new ExternalLearningReviewResult(ExternalLearningReviewOutcome.OwnLearning, submission);
            }

            if (submission.State != ExternalLearningState.Submitted)
            {
                return new ExternalLearningReviewResult(ExternalLearningReviewOutcome.AlreadyReviewed, submission);
            }

            if (accept)
            {
                // The credits are posted first, once per submission: if the request stops before the submission is
                // marked, it stays waiting and accepting it again completes it without posting the credits twice.
                await AwardAsync(new EducationCreditAwardRequest
                {
                    UserId = submission.UserId,
                    CreditTypeId = submission.CreditTypeId,
                    Amount = submission.Amount,
                    EarnedOn = submission.CompletedOn,
                    Source = CertificationsConstants.CreditSources.External,
                    SourceId = submission.ItemId,
                    Actor = actor,
                    Note = submission.Title,
                }, cancellationToken);

                // Posting the credits commits the session: read the submission again before changing it.
                submission = await _submissions.FindFreshByIdAsync(submissionId, cancellationToken);
                submission.State = ExternalLearningState.Accepted;
            }
            else
            {
                if (await HasCreditsAsync(submission, cancellationToken))
                {
                    return new ExternalLearningReviewResult(ExternalLearningReviewOutcome.CreditsPosted, submission);
                }

                submission.State = ExternalLearningState.Rejected;
            }

            submission.ReviewedBy = actor;
            submission.ReviewedUtc = _clock.UtcNow;
            submission.ReviewNote = note?.Trim();
            await _submissions.UpdateAsync(submission, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }

        foreach (var observer in _services.GetServices<IEducationCreditObserver>())
        {
            await observer.ReviewedAsync(submission, cancellationToken);
        }

        return new ExternalLearningReviewResult(ExternalLearningReviewOutcome.Reviewed, submission);
    }

    private async Task<bool> HasCreditsAsync(ExternalLearningSubmission submission, CancellationToken cancellationToken)
        => (await _ledger.ListForAccountAsync(submission.UserId, cancellationToken))
            .Any(entry => entry.Kind == CertificationsConstants.CreditSources.External && entry.SourceRef == submission.ItemId);

    /// <inheritdoc/>
    public async Task<ExternalLearningSubmission> FindExternalAsync(string submissionId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(submissionId) ? null : await _submissions.FindByIdAsync(submissionId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ExternalLearningSubmission>> ListExternalForUserAsync(string userId, CancellationToken cancellationToken = default)
        => _submissions.ListByUserAsync(userId, cancellationToken);

    /// <inheritdoc/>
    public Task<(int Count, IReadOnlyList<ExternalLearningSubmission> Items)> PageExternalAsync(ExternalLearningState? state, int skip, int take, CancellationToken cancellationToken = default)
        => _submissions.PageAsync(state, skip, take, cancellationToken);

    private async Task EvaluatePathsAsync(string creditTypeId, string userId, CancellationToken cancellationToken)
    {
        // The path service enrolls people and observes credits itself, so it is resolved when credits arrive.
        if (_services.GetService<ICertificationPathService>() is not { } paths)
        {
            return;
        }

        try
        {
            foreach (var path in await paths.ListByTargetAsync(creditTypeId, cancellationToken))
            {
                await paths.EvaluateAsync(path.ItemId, userId, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Checking the certification paths that ask for education credits failed; the certification upkeep checks again.");
        }
    }
}
