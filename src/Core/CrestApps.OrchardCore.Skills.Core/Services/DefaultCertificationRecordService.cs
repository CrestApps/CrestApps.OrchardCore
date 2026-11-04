using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Skills.Core.Services;

/// <summary>
/// The default <see cref="ICertificationRecordService"/>.
/// </summary>
internal sealed class DefaultCertificationRecordService : ICertificationRecordService
{
    private readonly CertificationRecordStore _store;
    private readonly CertificationStore _certificationStore;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public DefaultCertificationRecordService(
        CertificationRecordStore store,
        CertificationStore certificationStore,
        IClock clock,
        IStringLocalizer<DefaultCertificationRecordService> stringLocalizer)
    {
        _store = store;
        _certificationStore = certificationStore;
        _clock = clock;
        S = stringLocalizer;
    }

    public async Task<CertificationRecord> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        return await _store.FindByIdAsync(id, cancellationToken);
    }

    public async Task<SkillsResult> SaveAsync(CertificationRecord record, string actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        var result = new SkillsResult();

        if (string.IsNullOrEmpty(record.SubjectId))
        {
            result.Fail(S["Choose who holds the certificate."]);
        }

        var certification = string.IsNullOrEmpty(record.CertificationId)
            ? null
            : await _certificationStore.FindByIdAsync(record.CertificationId, cancellationToken);

        if (certification is null)
        {
            result.Fail(S["Choose a certification."]);
        }

        if (certification is not null && record.ExpiresOn is null && record.IssuedOn is { } issuedOn && certification.ValidityMonths is { } months)
        {
            record.ExpiresOn = issuedOn.AddMonths(months).AddDays(-1);
        }

        if (record.IssuedOn is not null && record.ExpiresOn is not null && record.ExpiresOn < record.IssuedOn)
        {
            result.Fail(S["The certificate expires before it was issued."]);
        }

        if (certification is not null && record.Status == CertificationStatus.Valid && certification.RequiresDocument && string.IsNullOrWhiteSpace(record.DocumentPath))
        {
            result.Fail(S["Add a copy of the certificate before marking it valid."]);
        }

        if (!result.Succeeded)
        {
            return result;
        }

        var now = _clock.UtcNow;
        var stored = string.IsNullOrEmpty(record.ItemId)
            ? null
            : await _store.FindByIdAsync(record.ItemId, cancellationToken);

        record.Number = string.IsNullOrWhiteSpace(record.Number) ? null : record.Number.Trim();
        record.SubjectName = string.IsNullOrWhiteSpace(record.SubjectName) ? null : record.SubjectName.Trim();

        if (stored is null)
        {
            record.CreatedUtc = now;
            await _store.CreateAsync(record, cancellationToken);

            return result;
        }

        // A record built outside this session (an API body, a recipe, an earlier request) is copied onto the stored
        // one: saving the outside object itself would add a second document with the same id.
        if (!ReferenceEquals(stored, record))
        {
            CopyTo(record, stored);
        }

        stored.ModifiedUtc = now;
        await _store.UpdateAsync(stored, cancellationToken);

        return result;
    }

    public async Task<SkillsResult> VerifyAsync(string id, string actor, CancellationToken cancellationToken = default)
    {
        var result = new SkillsResult();
        var record = await FindByIdAsync(id, cancellationToken);

        if (record is null)
        {
            result.Fail(S["The certificate doesn't exist."]);

            return result;
        }

        var previousStatus = record.Status;

        record.Status = CertificationStatus.Valid;
        record.VerifiedBy = actor;
        record.VerifiedUtc = _clock.UtcNow;

        var saved = await SaveAsync(record, actor, cancellationToken);

        if (!saved.Succeeded)
        {
            record.Status = previousStatus;
            record.VerifiedBy = null;
            record.VerifiedUtc = null;
        }

        return saved;
    }

    public async Task<SkillsResult> RevokeAsync(string id, string actor, string reason, CancellationToken cancellationToken = default)
    {
        var result = new SkillsResult();
        var record = await FindByIdAsync(id, cancellationToken);

        if (record is null)
        {
            result.Fail(S["The certificate doesn't exist."]);

            return result;
        }

        record.Status = CertificationStatus.Revoked;

        if (!string.IsNullOrWhiteSpace(reason))
        {
            record.Notes = string.IsNullOrWhiteSpace(record.Notes)
                ? reason.Trim()
                : $"{record.Notes}{Environment.NewLine}{reason.Trim()}";
        }

        return await SaveAsync(record, actor, cancellationToken);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var record = await FindByIdAsync(id, cancellationToken);

        if (record is null)
        {
            return false;
        }

        return await _store.DeleteAsync(record, cancellationToken);
    }

    public Task<IReadOnlyList<CertificationRecord>> GetForSubjectAsync(SkillSubject subject, CancellationToken cancellationToken = default)
    {
        if (subject.IsEmpty)
        {
            return Task.FromResult<IReadOnlyList<CertificationRecord>>([]);
        }

        return _store.ListForSubjectAsync(subject, cancellationToken);
    }

    public async Task<bool> IsValidOnAsync(SkillSubject subject, string certificationId, DateOnly date, CancellationToken cancellationToken = default)
    {
        var records = await GetForSubjectAsync(subject, cancellationToken);

        return records.Any(record => record.CertificationId == certificationId && record.IsValidOn(date));
    }

    public Task<(int Count, IReadOnlyList<CertificationRecord> Items)> PageAsync(CertificationRecordQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        return _store.PageAsync(query, skip, take, cancellationToken);
    }

    private static void CopyTo(CertificationRecord source, CertificationRecord target)
    {
        target.SubjectKind = source.SubjectKind;
        target.SubjectId = source.SubjectId;
        target.SubjectName = source.SubjectName;
        target.CertificationId = source.CertificationId;
        target.Number = source.Number;
        target.IssuedOn = source.IssuedOn;
        target.ExpiresOn = source.ExpiresOn;
        target.DocumentPath = source.DocumentPath;
        target.Status = source.Status;
        target.VerifiedBy = source.VerifiedBy;
        target.VerifiedUtc = source.VerifiedUtc;
        target.Notes = source.Notes;
        target.Properties = source.Properties;
    }
}
