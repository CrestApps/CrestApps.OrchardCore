using System.Linq.Expressions;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.BackgroundTasks;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using OrchardCore.Documents;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// The default <see cref="IOmnichannelContactPhoneLookup"/>, reading <see cref="OmnichannelContactPhoneNumberIndex"/>.
/// </summary>
/// <remarks>
/// A record saved before the phone number index existed has no rows in it until
/// <see cref="ContactPhoneNumberIndexBackfillBackgroundTask"/> indexes it again. Until that has finished, every lookup
/// also reads the first cell and home columns of <see cref="OmnichannelContactIndex"/>, the only numbers those records
/// were found by before, so no record that used to be found goes missing in the meantime.
/// </remarks>
internal sealed class OmnichannelContactPhoneLookup : IOmnichannelContactPhoneLookup
{
    private readonly ISession _session;
    private readonly IDocumentManager<OmnichannelBackfillState> _backfillState;

    private bool? _backfilled;

    public OmnichannelContactPhoneLookup(
        ISession session,
        IDocumentManager<OmnichannelBackfillState> backfillState)
    {
        _session = session;
        _backfillState = backfillState;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OmnichannelContactPhoneNumberIndex>> FindByNumbersAsync(
        IReadOnlyCollection<string> normalizedPhoneNumbers,
        OmnichannelContactVersion version,
        IReadOnlyCollection<string> contentTypes = null,
        CancellationToken cancellationToken = default)
    {
        var numbers = Distinct(normalizedPhoneNumbers);

        if (numbers.Length == 0 || contentTypes is { Count: 0 })
        {
            return [];
        }

        var types = contentTypes?.ToArray();
        var requested = new HashSet<string>(numbers, StringComparer.Ordinal);
        var rows = new List<OmnichannelContactPhoneNumberIndex>();

        // One indexed column per query, in fixed-size batches; see OmnichannelContactDuplicateLookupService.LookupBatchSize.
        foreach (var batch in OmnichannelContactDuplicateLookupService.ToLookupBatches(numbers))
        {
            rows.AddRange(await _session
                .QueryIndex(ByNumbers(batch, version, types))
                .ListAsync(cancellationToken));
        }

        if (!await IsBackfilledAsync())
        {
            foreach (var batch in OmnichannelContactDuplicateLookupService.ToLookupBatches(numbers))
            {
                var byCell = await _session
                    .QueryIndex(LegacyByCell(batch, version, types))
                    .ListAsync(cancellationToken);

                var byHome = await _session
                    .QueryIndex(LegacyByHome(batch, version, types))
                    .ListAsync(cancellationToken);

                rows.AddRange(byCell.Concat(byHome).SelectMany(ToPhoneNumberRows));
            }
        }

        return DistinctRows(rows.Where(row => requested.Contains(row.NormalizedPhoneNumber)));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<OmnichannelContactPhoneNumberIndex>> GetNumbersAsync(
        IReadOnlyCollection<string> contentItemIds,
        OmnichannelContactVersion version,
        CancellationToken cancellationToken = default)
    {
        var ids = Distinct(contentItemIds);

        if (ids.Length == 0)
        {
            return [];
        }

        var rows = new List<OmnichannelContactPhoneNumberIndex>();

        foreach (var batch in OmnichannelContactDuplicateLookupService.ToLookupBatches(ids))
        {
            rows.AddRange(await _session
                .QueryIndex(ByItems(batch, version))
                .ListAsync(cancellationToken));
        }

        if (!await IsBackfilledAsync())
        {
            foreach (var batch in OmnichannelContactDuplicateLookupService.ToLookupBatches(ids))
            {
                var contacts = await _session
                    .QueryIndex(LegacyByItems(batch, version))
                    .ListAsync(cancellationToken);

                rows.AddRange(contacts.SelectMany(ToPhoneNumberRows));
            }
        }

        return DistinctRows(rows);
    }

    private async Task<bool> IsBackfilledAsync()
    {
        if (_backfilled is null)
        {
            var state = await _backfillState.GetOrCreateImmutableAsync();

            _backfilled = state.Completed.Contains(ContactPhoneNumberIndexBackfillBackgroundTask.BackfillName);
        }

        return _backfilled.Value;
    }

    // Each version has its own predicate rather than one built from conditions, so every query is a plain seek on
    // (NormalizedPhoneNumber, Published, Latest) or (ContentItemId, Published, Latest). Every row is published or
    // latest, so Any needs no version condition at all.
    private static Expression<Func<OmnichannelContactPhoneNumberIndex, bool>> ByNumbers(
        string[] numbers,
        OmnichannelContactVersion version,
        string[] types)
        => (version, types is null) switch
        {
            (OmnichannelContactVersion.Published, true) => index => index.NormalizedPhoneNumber.IsIn(numbers) && index.Published,
            (OmnichannelContactVersion.Published, false) => index => index.NormalizedPhoneNumber.IsIn(numbers) && index.Published && index.ContentType.IsIn(types),
            (OmnichannelContactVersion.Latest, true) => index => index.NormalizedPhoneNumber.IsIn(numbers) && index.Latest,
            (OmnichannelContactVersion.Latest, false) => index => index.NormalizedPhoneNumber.IsIn(numbers) && index.Latest && index.ContentType.IsIn(types),
            (_, true) => index => index.NormalizedPhoneNumber.IsIn(numbers),
            (_, false) => index => index.NormalizedPhoneNumber.IsIn(numbers) && index.ContentType.IsIn(types),
        };

    private static Expression<Func<OmnichannelContactPhoneNumberIndex, bool>> ByItems(
        string[] ids,
        OmnichannelContactVersion version)
        => version switch
        {
            OmnichannelContactVersion.Published => index => index.ContentItemId.IsIn(ids) && index.Published,
            OmnichannelContactVersion.Latest => index => index.ContentItemId.IsIn(ids) && index.Latest,
            _ => index => index.ContentItemId.IsIn(ids),
        };

    private static Expression<Func<OmnichannelContactIndex, bool>> LegacyByCell(
        string[] numbers,
        OmnichannelContactVersion version,
        string[] types)
        => (version, types is null) switch
        {
            (OmnichannelContactVersion.Published, true) => index => index.NormalizedPrimaryCellPhoneNumber.IsIn(numbers) && index.Published,
            (OmnichannelContactVersion.Published, false) => index => index.NormalizedPrimaryCellPhoneNumber.IsIn(numbers) && index.Published && index.ContentType.IsIn(types),
            (OmnichannelContactVersion.Latest, true) => index => index.NormalizedPrimaryCellPhoneNumber.IsIn(numbers) && index.Latest,
            (OmnichannelContactVersion.Latest, false) => index => index.NormalizedPrimaryCellPhoneNumber.IsIn(numbers) && index.Latest && index.ContentType.IsIn(types),
            (_, true) => index => index.NormalizedPrimaryCellPhoneNumber.IsIn(numbers),
            (_, false) => index => index.NormalizedPrimaryCellPhoneNumber.IsIn(numbers) && index.ContentType.IsIn(types),
        };

    private static Expression<Func<OmnichannelContactIndex, bool>> LegacyByHome(
        string[] numbers,
        OmnichannelContactVersion version,
        string[] types)
        => (version, types is null) switch
        {
            (OmnichannelContactVersion.Published, true) => index => index.NormalizedPrimaryHomePhoneNumber.IsIn(numbers) && index.Published,
            (OmnichannelContactVersion.Published, false) => index => index.NormalizedPrimaryHomePhoneNumber.IsIn(numbers) && index.Published && index.ContentType.IsIn(types),
            (OmnichannelContactVersion.Latest, true) => index => index.NormalizedPrimaryHomePhoneNumber.IsIn(numbers) && index.Latest,
            (OmnichannelContactVersion.Latest, false) => index => index.NormalizedPrimaryHomePhoneNumber.IsIn(numbers) && index.Latest && index.ContentType.IsIn(types),
            (_, true) => index => index.NormalizedPrimaryHomePhoneNumber.IsIn(numbers),
            (_, false) => index => index.NormalizedPrimaryHomePhoneNumber.IsIn(numbers) && index.ContentType.IsIn(types),
        };

    private static Expression<Func<OmnichannelContactIndex, bool>> LegacyByItems(
        string[] ids,
        OmnichannelContactVersion version)
        => version switch
        {
            OmnichannelContactVersion.Published => index => index.ContentItemId.IsIn(ids) && index.Published,
            OmnichannelContactVersion.Latest => index => index.ContentItemId.IsIn(ids) && index.Latest,
            _ => index => index.ContentItemId.IsIn(ids),
        };

    private static IEnumerable<OmnichannelContactPhoneNumberIndex> ToPhoneNumberRows(OmnichannelContactIndex contact)
    {
        if (!string.IsNullOrEmpty(contact.NormalizedPrimaryCellPhoneNumber))
        {
            yield return ToPhoneNumberRow(contact, "Cell", contact.NormalizedPrimaryCellPhoneNumber);
        }

        if (!string.IsNullOrEmpty(contact.NormalizedPrimaryHomePhoneNumber))
        {
            yield return ToPhoneNumberRow(contact, "Home", contact.NormalizedPrimaryHomePhoneNumber);
        }
    }

    private static OmnichannelContactPhoneNumberIndex ToPhoneNumberRow(OmnichannelContactIndex contact, string phoneType, string number)
        => new()
        {
            ContentItemId = contact.ContentItemId,
            ContentType = contact.ContentType,
            IsConverted = contact.IsConverted,
            Published = contact.Published,
            Latest = contact.Latest,
            PhoneType = phoneType,
            NormalizedPhoneNumber = number,
        };

    // A record matched on its published and its latest version, or by both indexes while the backfill runs, is the
    // same number of the same record: it is returned once, as the first row read.
    private static OmnichannelContactPhoneNumberIndex[] DistinctRows(IEnumerable<OmnichannelContactPhoneNumberIndex> rows)
        => rows
            .Where(row => !string.IsNullOrEmpty(row.ContentItemId) && !string.IsNullOrEmpty(row.NormalizedPhoneNumber))
            .DistinctBy(row => (row.ContentItemId, row.NormalizedPhoneNumber))
            .ToArray();

    private static string[] Distinct(IReadOnlyCollection<string> values)
        => values is null
            ? []
            : values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
}
