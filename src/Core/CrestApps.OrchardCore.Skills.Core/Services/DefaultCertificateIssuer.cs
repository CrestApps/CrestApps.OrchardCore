using CrestApps.OrchardCore.Numbering;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking.Distributed;
using YesSql;

namespace CrestApps.OrchardCore.Skills.Core.Services;

/// <summary>
/// Issues and renews certificates under a lock per source (or per renewed certificate), committing inside the lock so
/// a second caller waiting on it finds the certificate, and telling the observers after the commit.
/// </summary>
internal sealed class DefaultCertificateIssuer : ICertificateIssuer
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(1);

    private readonly CertificationRecordStore _store;
    private readonly CertificationStore _certificationStore;
    private readonly ICertificationRecordService _records;
    private readonly INumberSequence _numberSequence;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IEnumerable<ICertificationRecordObserver> _observers;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public DefaultCertificateIssuer(
        CertificationRecordStore store,
        CertificationStore certificationStore,
        ICertificationRecordService records,
        INumberSequence numberSequence,
        IDistributedLock distributedLock,
        ISession session,
        IEnumerable<ICertificationRecordObserver> observers,
        ILogger<DefaultCertificateIssuer> logger,
        IStringLocalizer<DefaultCertificateIssuer> stringLocalizer)
    {
        _store = store;
        _certificationStore = certificationStore;
        _records = records;
        _numberSequence = numberSequence;
        _distributedLock = distributedLock;
        _session = session;
        _observers = observers;
        _logger = logger;
        S = stringLocalizer;
    }

    public async Task<CertificateIssueResult> IssueAsync(CertificateIssueRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = new CertificateIssueResult();
        var certification = await ValidateAsync(request, result, cancellationToken);

        if (certification is null)
        {
            return result;
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(
            $"Skills:IssueCertificate:{request.CertificationId}:{request.Source}:{request.SourceId}",
            _lockTimeout,
            _lockExpiration);

        if (!locked)
        {
            result.Fail(S["Another request is issuing this certificate. Try again in a moment."]);

            return result;
        }

        CertificationRecord record;

        await using (locker)
        {
            var existing = await _store.FindIssuedAsync(request.CertificationId, request.Source, request.SourceId, cancellationToken);

            if (existing is not null)
            {
                result.Record = existing;
                result.AlreadyIssued = true;

                return result;
            }

            record = await CreateAsync(request, certification, previousRecordId: null, result, cancellationToken);

            if (record is null)
            {
                return result;
            }

            await _session.SaveChangesAsync(cancellationToken);
        }

        result.Record = record;

        var context = new CertificationRecordContext
        {
            Record = record,
            Certification = certification,
            Actor = request.Actor,
        };

        await CertificationRecordObservers.NotifyAsync(_observers, observer => observer.IssuedAsync(context, cancellationToken), record, _logger);

        return result;
    }

    public async Task<CertificateIssueResult> RenewAsync(string previousRecordId, CertificateIssueRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = new CertificateIssueResult();

        if (string.IsNullOrEmpty(previousRecordId))
        {
            result.Fail(S["Choose the certificate to renew."]);

            return result;
        }

        var certification = await ValidateAsync(request, result, cancellationToken);

        if (certification is null)
        {
            return result;
        }

        var (locker, locked) = await _distributedLock.TryAcquireLockAsync($"Skills:RenewCertificate:{previousRecordId}", _lockTimeout, _lockExpiration);

        if (!locked)
        {
            result.Fail(S["Another request is renewing this certificate. Try again in a moment."]);

            return result;
        }

        CertificationRecord record;
        CertificationRecord previous;

        await using (locker)
        {
            previous = await _store.FindByIdAsync(previousRecordId, cancellationToken);

            if (previous is null || previous.CertificationId != request.CertificationId)
            {
                result.Fail(S["The certificate to renew doesn't exist."]);

                return result;
            }

            if (!string.IsNullOrEmpty(previous.RenewedByRecordId))
            {
                result.Record = await _store.FindByIdAsync(previous.RenewedByRecordId, cancellationToken);
                result.AlreadyIssued = true;

                if (result.Record is null)
                {
                    result.Fail(S["The certificate was renewed, but the renewal no longer exists."]);
                }

                return result;
            }

            if (previous.Status == CertificationStatus.Revoked)
            {
                result.Fail(S["A revoked certificate cannot be renewed."]);

                return result;
            }

            record = await CreateAsync(request, certification, previous.ItemId, result, cancellationToken);

            if (record is null)
            {
                return result;
            }

            previous.RenewedByRecordId = record.ItemId;
            previous.ModifiedUtc = record.CreatedUtc;
            await _store.UpdateAsync(previous, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);
        }

        result.Record = record;

        var context = new CertificationRecordContext
        {
            Record = record,
            Certification = certification,
            PreviousRecord = previous,
            Actor = request.Actor,
        };

        await CertificationRecordObservers.NotifyAsync(_observers, observer => observer.RenewedAsync(context, cancellationToken), record, _logger);

        return result;
    }

    public Task<SkillsResult> RevokeAsync(string recordId, string actor, string reason, CancellationToken cancellationToken = default)
    {
        return _records.RevokeAsync(recordId, actor, reason, cancellationToken);
    }

    public Task<CertificationRecord> FindByVerificationCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = CertificateVerificationCodes.Normalize(code);

        return normalized is null
            ? Task.FromResult<CertificationRecord>(null)
            : _store.FindByVerificationCodeAsync(normalized, cancellationToken);
    }

    public Task<CertificationRecord> FindIssuedAsync(string certificationId, string source, string sourceId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(certificationId) || string.IsNullOrEmpty(source) || string.IsNullOrEmpty(sourceId))
        {
            return Task.FromResult<CertificationRecord>(null);
        }

        return _store.FindIssuedAsync(certificationId, source, sourceId, cancellationToken);
    }

    private async Task<Certification> ValidateAsync(CertificateIssueRequest request, CertificateIssueResult result, CancellationToken cancellationToken)
    {
        if (request.Subject.IsEmpty)
        {
            result.Fail(S["Choose who earns the certificate."]);
        }

        if (string.IsNullOrWhiteSpace(request.Source) || string.IsNullOrWhiteSpace(request.SourceId))
        {
            result.Fail(S["Say what earned the certificate."]);
        }

        var certification = string.IsNullOrEmpty(request.CertificationId)
            ? null
            : await _certificationStore.FindByIdAsync(request.CertificationId, cancellationToken);

        if (certification is null || !certification.Enabled)
        {
            result.Fail(S["The certification doesn't exist or is not in use."]);
        }

        return result.Errors.Count == 0 ? certification : null;
    }

    private async Task<CertificationRecord> CreateAsync(
        CertificateIssueRequest request,
        Certification certification,
        string previousRecordId,
        CertificateIssueResult result,
        CancellationToken cancellationToken)
    {
        var record = new CertificationRecord
        {
            SubjectKind = request.Subject.Kind,
            SubjectId = request.Subject.Id,
            SubjectName = request.SubjectName,
            CertificationId = certification.ItemId,
            IssuedOn = request.IssuedOn,
            ExpiresOn = request.ExpiresOn,
            Status = CertificationStatus.Valid,
            VerifiedBy = request.Actor,
            Source = request.Source,
            SourceId = request.SourceId,
            Evidence = request.Evidence,
            PreviousRecordId = previousRecordId,
            VerificationCode = ReadableCodes.Generate(SkillsConstants.VerificationCodeLength),
        };

        if (certification.GenerateNumbers)
        {
            var prefix = string.IsNullOrWhiteSpace(certification.NumberPrefix)
                ? SkillsConstants.DefaultCertificateNumberPrefix
                : certification.NumberPrefix.Trim();

            record.Number = await _numberSequence.NextAsync(SkillsConstants.CertificateNumberSeries + prefix, prefix, 6, new NumberSequenceOptions
            {
                // Certificates recorded by hand with the same prefix keep their numbers: the series starts after them.
                SeedAsync = async token => NumberSequenceExtensions.HighestNumber(await _store.ListNumbersAsync(prefix, token), prefix),
            }, cancellationToken);
        }

        var saved = await _records.SaveAsync(record, request.Actor, cancellationToken);

        foreach (var error in saved.Errors)
        {
            result.Fail(error);
        }

        if (!saved.Succeeded)
        {
            return null;
        }

        record.VerifiedUtc = record.CreatedUtc;

        return record;
    }
}
