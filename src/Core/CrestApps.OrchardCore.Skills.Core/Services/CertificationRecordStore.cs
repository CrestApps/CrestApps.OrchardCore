using CrestApps.OrchardCore.Skills.Core.Indexes;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Skills.Core.Services;

/// <summary>
/// Stores certification records.
/// </summary>
internal sealed class CertificationRecordStore : DocumentCatalog<CertificationRecord, CertificationRecordIndex>
{
    public CertificationRecordStore(ISession session)
        : base(session)
    {
        CollectionName = SkillsConstants.CollectionName;
    }

    public async Task<IReadOnlyList<CertificationRecord>> ListForSubjectAsync(SkillSubject subject, CancellationToken cancellationToken = default)
    {
        var kind = subject.Kind;
        var legacyKind = SkillSubjectKind.ToLegacyValue(kind);
        var id = subject.Id;

        // A row the deferred re-index has not reached yet only has the number the kind was stored as.
        var records = await Session.Query<CertificationRecord, CertificationRecordIndex>(
            index => (index.SubjectKindName == kind || (index.SubjectKindName == null && index.SubjectKind == legacyKind)) && index.SubjectId == id,
            collection: CollectionName)
            .OrderByDescending(index => index.ExpiresOn)
            .ListAsync(cancellationToken);

        return records.ToArray();
    }

    public async Task<IReadOnlyList<string>> ListValidSubjectIdsAsync(string certificationId, string kind, int dateKey, CancellationToken cancellationToken = default)
    {
        var legacyKind = SkillSubjectKind.ToLegacyValue(kind);

        var rows = await Session.QueryIndex<CertificationRecordIndex>(
            index => index.CertificationId == certificationId
                && (index.SubjectKindName == kind || (index.SubjectKindName == null && index.SubjectKind == legacyKind))
                && index.Status == CertificationStatus.Valid
                && (index.IssuedOn == null || index.IssuedOn <= dateKey)
                && (index.ExpiresOn == null || index.ExpiresOn >= dateKey),
            collection: CollectionName)
            .ListAsync(cancellationToken);

        return rows.Select(row => row.SubjectId).Distinct(StringComparer.Ordinal).ToArray();
    }

    public async Task<IReadOnlyList<CertificationRecord>> ListExpiringAsync(int onOrBeforeKey, CancellationToken cancellationToken = default)
    {
        var records = await Session.Query<CertificationRecord, CertificationRecordIndex>(
            index => index.Status == CertificationStatus.Valid && index.ExpiresOn != null && index.ExpiresOn <= onOrBeforeKey,
            collection: CollectionName)
            .ListAsync(cancellationToken);

        return records.ToArray();
    }

    public async Task<(int Count, IReadOnlyList<CertificationRecord> Items)> PageAsync(CertificationRecordQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        var results = Session.Query<CertificationRecord, CertificationRecordIndex>(collection: CollectionName);

        if (!string.IsNullOrWhiteSpace(query?.Search))
        {
            var search = query.Search.Trim();

            results = results.Where(index => index.SubjectName.Contains(search) || index.Number.Contains(search));
        }

        if (!string.IsNullOrEmpty(query?.CertificationId))
        {
            var certificationId = query.CertificationId;

            results = results.Where(index => index.CertificationId == certificationId);
        }

        if (query?.Status is { } status)
        {
            results = results.Where(index => index.Status == status);
        }

        if (query?.ExpiresOnOrBefore is { } expiresOnOrBefore)
        {
            var key = SkillsDates.ToKey(expiresOnOrBefore);

            results = results.Where(index => index.ExpiresOn != null && index.ExpiresOn <= key);
        }

        if (query is not null && !query.Subject.IsEmpty)
        {
            var kind = query.Subject.Kind;
            var legacyKind = SkillSubjectKind.ToLegacyValue(kind);
            var id = query.Subject.Id;

            results = results.Where(index => (index.SubjectKindName == kind || (index.SubjectKindName == null && index.SubjectKind == legacyKind)) && index.SubjectId == id);
        }

        var count = await results.CountAsync(cancellationToken);
        var page = await results.OrderBy(index => index.ExpiresOn).ThenBy(index => index.SubjectName).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, page.ToArray());
    }

    /// <summary>
    /// Finds the certificate issued for a certification, source and source id that has not been revoked.
    /// </summary>
    /// <param name="certificationId">The certification.</param>
    /// <param name="source">The source.</param>
    /// <param name="sourceId">The source id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The certificate, or <see langword="null"/>.</returns>
    public Task<CertificationRecord> FindIssuedAsync(string certificationId, string source, string sourceId, CancellationToken cancellationToken = default)
    {
        return Session.Query<CertificationRecord, CertificationRecordIndex>(
            index => index.CertificationId == certificationId
                && index.Source == source
                && index.SourceId == sourceId
                && index.Status != CertificationStatus.Revoked,
            collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Finds a certificate by its verification code.
    /// </summary>
    /// <param name="code">The code, as it is stored.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The certificate, or <see langword="null"/>.</returns>
    public Task<CertificationRecord> FindByVerificationCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return Session.Query<CertificationRecord, CertificationRecordIndex>(index => index.VerificationCode == code, collection: CollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Lists the numbers of the certificates whose number starts with a prefix, to seed a number series.
    /// </summary>
    /// <param name="prefix">The prefix.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The numbers.</returns>
    public async Task<IReadOnlyList<string>> ListNumbersAsync(string prefix, CancellationToken cancellationToken = default)
    {
        var rows = await Session.QueryIndex<CertificationRecordIndex>(index => index.Number.StartsWith(prefix), collection: CollectionName)
            .ListAsync(cancellationToken);

        return rows.Select(row => row.Number).ToArray();
    }

    public async Task<IReadOnlyList<CertificationRecord>> ListByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var records = await Session.Query<CertificationRecord, CertificationRecordIndex>(index => index.ItemId.IsIn(ids), collection: CollectionName)
            .ListAsync(cancellationToken);

        return records.ToArray();
    }
}
