using CrestApps.OrchardCore.Learning.Certifications.Models;
using CrestApps.OrchardCore.Learning.Certifications.Services;
using CrestApps.OrchardCore.Ledgers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
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
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public DefaultEducationCreditService(
        EducationCreditTypeStore types,
        ExternalLearningSubmissionStore submissions,
        IQuantityLedger<EducationCreditLedger> ledger,
        IServiceProvider services,
        ISession session,
        IClock clock,
        ILogger<DefaultEducationCreditService> logger,
        IStringLocalizer<DefaultEducationCreditService> stringLocalizer)
    {
        _types = types;
        _submissions = submissions;
        _ledger = ledger;
        _services = services;
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

        if (string.IsNullOrEmpty(request.UserId) || string.IsNullOrEmpty(request.CreditTypeId) || request.Amount <= 0)
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
            IdempotencyKey = $"{request.Source}:{request.SourceId}:{request.CreditTypeId}",
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
    public async Task<ExternalLearningSubmission> AcceptExternalAsync(string submissionId, string actor, string note, CancellationToken cancellationToken = default)
    {
        var submission = await FindExternalAsync(submissionId, cancellationToken);

        if (submission is null || submission.State != ExternalLearningState.Submitted)
        {
            return null;
        }

        submission.State = ExternalLearningState.Accepted;
        submission.ReviewedBy = actor;
        submission.ReviewedUtc = _clock.UtcNow;
        submission.ReviewNote = note?.Trim();
        await _submissions.UpdateAsync(submission, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

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

        foreach (var observer in _services.GetServices<IEducationCreditObserver>())
        {
            await observer.ReviewedAsync(submission, cancellationToken);
        }

        return submission;
    }

    /// <inheritdoc/>
    public async Task<ExternalLearningSubmission> RejectExternalAsync(string submissionId, string actor, string note, CancellationToken cancellationToken = default)
    {
        var submission = await FindExternalAsync(submissionId, cancellationToken);

        if (submission is null || submission.State != ExternalLearningState.Submitted)
        {
            return null;
        }

        submission.State = ExternalLearningState.Rejected;
        submission.ReviewedBy = actor;
        submission.ReviewedUtc = _clock.UtcNow;
        submission.ReviewNote = note?.Trim();
        await _submissions.UpdateAsync(submission, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        foreach (var observer in _services.GetServices<IEducationCreditObserver>())
        {
            await observer.ReviewedAsync(submission, cancellationToken);
        }

        return submission;
    }

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
