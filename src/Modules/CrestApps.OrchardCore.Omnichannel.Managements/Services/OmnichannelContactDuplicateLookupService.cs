using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Queries stored omnichannel contacts for existing normalized phone numbers.
/// Phone numbers are compared in E.164 format, across every number of each record; a number saved in a form that
/// could not be read as E.164 is compared by its national digits, which the contact index keeps for the first cell
/// and home numbers only.
/// </summary>
public sealed class OmnichannelContactDuplicateLookupService : IOmnichannelContactDuplicateLookupService
{
    // Every number lookup compares one column per query, never an OR across columns: an OR keeps SQL Server from
    // seeking the column's own index, so it scans the whole contact index instead. The numbers go in small batches,
    // because every match is read back through the clustered index and a long IN list estimates enough matches that
    // a scan looks cheaper again. A short batch is padded to the full size so every lookup sends the same command
    // text, and the database compiles and keeps one plan for it rather than one per list length.
    internal const int LookupBatchSize = 50;

    private readonly ISession _session;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly IOmnichannelContactPhoneLookup _phoneLookup;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactDuplicateLookupService"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="phoneNumberService">The phone number service for E.164 formatting.</param>
    /// <param name="phoneLookup">The lookup that finds records by any of their phone numbers.</param>
    public OmnichannelContactDuplicateLookupService(
        ISession session,
        IPhoneNumberService phoneNumberService,
        IOmnichannelContactPhoneLookup phoneLookup)
    {
        _session = session;
        _phoneNumberService = phoneNumberService;
        _phoneLookup = phoneLookup;
    }

    /// <inheritdoc />
    public async Task<HashSet<string>> GetExistingNormalizedPhoneNumbersAsync(
        IEnumerable<string> phoneNumbers,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(phoneNumbers);

        var normalizedPhoneNumbers = phoneNumbers
            .Where(phoneNumber => !string.IsNullOrWhiteSpace(phoneNumber))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedPhoneNumbers.Length == 0)
        {
            return [];
        }

        var existingPhoneNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var match in await _phoneLookup.FindByNumbersAsync(normalizedPhoneNumbers, OmnichannelContactVersion.Published, cancellationToken: cancellationToken))
        {
            existingPhoneNumbers.Add(match.NormalizedPhoneNumber);
        }

        // A number not found in E.164 may still be one saved in a form that could not be read as E.164, which the
        // contact index keeps as national digits.
        var missingPhoneNumbers = normalizedPhoneNumbers
            .Where(phoneNumber => !existingPhoneNumbers.Contains(phoneNumber))
            .ToArray();

        if (missingPhoneNumbers.Length == 0)
        {
            return existingPhoneNumbers;
        }

        var cellPhoneMatches = new List<OmnichannelContactIndex>();
        var homePhoneMatches = new List<OmnichannelContactIndex>();

        // One query per column; see LookupBatchSize.
        foreach (var chunk in ToLookupBatches(missingPhoneNumbers))
        {
            cellPhoneMatches.AddRange(await _session.QueryIndex<OmnichannelContactIndex>(
                    index => index.Published &&
                        index.PrimaryCellPhoneNumber.IsIn(chunk))
                .ListAsync(cancellationToken));
            homePhoneMatches.AddRange(await _session.QueryIndex<OmnichannelContactIndex>(
                    index => index.Published &&
                        index.PrimaryHomePhoneNumber.IsIn(chunk))
                .ListAsync(cancellationToken));
        }

        AddLegacyMatches(existingPhoneNumbers, cellPhoneMatches, missingPhoneNumbers, static match => match.PrimaryCellPhoneNumber);
        AddLegacyMatches(existingPhoneNumbers, homePhoneMatches, missingPhoneNumbers, static match => match.PrimaryHomePhoneNumber);

        return existingPhoneNumbers;
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string[]>> GetPhoneNumberOwnersAsync(
        IReadOnlyCollection<string> phoneNumberKeys,
        Func<IOmnichannelContactMatch, bool> include,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(phoneNumberKeys);

        var keys = phoneNumberKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (keys.Length == 0)
        {
            return [];
        }

        var phoneOwners = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        // Every number of every record, whatever type each is saved as.
        foreach (var match in await _phoneLookup.FindByNumbersAsync(keys, OmnichannelContactVersion.Published, cancellationToken: cancellationToken))
        {
            if (include is null || include(match))
            {
                AddPhoneOwner(phoneOwners, match.NormalizedPhoneNumber, match.ContentItemId);
            }
        }

        // A number saved in a form that could not be read as E.164 is kept by the contact index as national digits,
        // for the first cell and home numbers. One query per column; see LookupBatchSize. A record found by more than
        // one column is counted once.
        var seenDocuments = new HashSet<long>();

        foreach (var chunk in ToLookupBatches(keys))
        {
            var matches = new List<OmnichannelContactIndex>();

            matches.AddRange(await _session.QueryIndex<OmnichannelContactIndex>(index =>
                    index.Published && index.PrimaryCellPhoneNumber.IsIn(chunk))
                .ListAsync(cancellationToken));
            matches.AddRange(await _session.QueryIndex<OmnichannelContactIndex>(index =>
                    index.Published && index.PrimaryHomePhoneNumber.IsIn(chunk))
                .ListAsync(cancellationToken));

            foreach (var index in matches)
            {
                if (!seenDocuments.Add(index.Id) || (include is not null && !include(index)))
                {
                    continue;
                }

                AddPhoneOwners(phoneOwners, index.ContentItemId, index.NormalizedPrimaryCellPhoneNumber, index.PrimaryCellPhoneNumber);
                AddPhoneOwners(phoneOwners, index.ContentItemId, index.NormalizedPrimaryHomePhoneNumber, index.PrimaryHomePhoneNumber);
            }
        }

        var requested = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);

        return phoneOwners
            .Where(pair => requested.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Splits <paramref name="values"/> into batches of exactly <see cref="LookupBatchSize"/>, repeating the last
    /// value of a short batch to fill it. A repeated value matches the same rows again, so callers that merge matches
    /// by record see no difference.
    /// </summary>
    /// <param name="values">The values to look up; empty yields no batch.</param>
    internal static IEnumerable<string[]> ToLookupBatches(IReadOnlyList<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        for (var start = 0; start < values.Count; start += LookupBatchSize)
        {
            var last = Math.Min(start + LookupBatchSize, values.Count) - 1;
            var batch = new string[LookupBatchSize];

            for (var i = 0; i < LookupBatchSize; i++)
            {
                batch[i] = values[Math.Min(start + i, last)];
            }

            yield return batch;
        }
    }

    private void AddPhoneFromIndex(HashSet<string> phoneNumbers, string normalizedValue, string rawValue)
    {
        if (!string.IsNullOrWhiteSpace(normalizedValue))
        {
            phoneNumbers.Add(normalizedValue);
        }
        else if (!string.IsNullOrWhiteSpace(rawValue))
        {
            var normalized = NormalizePhoneNumber(rawValue);

            if (!string.IsNullOrWhiteSpace(normalized))
            {
                phoneNumbers.Add(normalized);
            }
        }
    }

    private void AddPhoneOwners(
        IDictionary<string, HashSet<string>> phoneOwners,
        string contentItemId,
        string normalizedValue,
        string rawValue)
    {
        if (string.IsNullOrWhiteSpace(contentItemId))
        {
            return;
        }

        AddPhoneOwner(phoneOwners, normalizedValue, contentItemId);

        if (!string.IsNullOrWhiteSpace(rawValue))
        {
            AddPhoneOwner(phoneOwners, NormalizePhoneNumber(rawValue), contentItemId);
            AddPhoneOwner(phoneOwners, rawValue.Trim(), contentItemId);
        }
    }

    private static void AddPhoneOwner(
        IDictionary<string, HashSet<string>> phoneOwners,
        string phoneNumber,
        string contentItemId)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return;
        }

        if (!phoneOwners.TryGetValue(phoneNumber, out var owners))
        {
            owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            phoneOwners[phoneNumber] = owners;
        }

        owners.Add(contentItemId);
    }

    internal static void AddLegacyMatches(
        HashSet<string> existingPhoneNumbers,
        IEnumerable<OmnichannelContactIndex> matches,
        IEnumerable<string> missingPhoneNumbers,
        Func<OmnichannelContactIndex, string> phoneSelector)
    {
        foreach (var match in matches)
        {
            var rawPhone = phoneSelector(match);

            if (string.IsNullOrWhiteSpace(rawPhone) ||
                !missingPhoneNumbers.Contains(rawPhone, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            existingPhoneNumbers.Add(rawPhone);
        }
    }

    internal string NormalizePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return string.Empty;
        }

        _phoneNumberService.TryParse(phoneNumber, null, out var canonicalNumber);

        return PhoneNumberComparisonKey.For(canonicalNumber, phoneNumber);
    }
}
