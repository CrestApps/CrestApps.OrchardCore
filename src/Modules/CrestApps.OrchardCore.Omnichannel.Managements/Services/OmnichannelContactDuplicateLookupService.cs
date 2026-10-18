using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.PhoneNumbers;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Queries stored omnichannel contacts for existing normalized phone numbers.
/// Phone numbers are compared in E.164 format.
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

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactDuplicateLookupService"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="phoneNumberService">The phone number service for E.164 formatting.</param>
    public OmnichannelContactDuplicateLookupService(
        ISession session,
        IPhoneNumberService phoneNumberService)
    {
        _session = session;
        _phoneNumberService = phoneNumberService;
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
        var cellPhoneMatches = new List<OmnichannelContactIndex>();
        var homePhoneMatches = new List<OmnichannelContactIndex>();

        // One query per column; see LookupBatchSize.
        foreach (var chunk in ToLookupBatches(normalizedPhoneNumbers))
        {
            cellPhoneMatches.AddRange(await _session.QueryIndex<OmnichannelContactIndex>(
                    index => index.Published &&
                        index.NormalizedPrimaryCellPhoneNumber.IsIn(chunk))
                .ListAsync(cancellationToken));
            homePhoneMatches.AddRange(await _session.QueryIndex<OmnichannelContactIndex>(
                    index => index.Published &&
                        index.NormalizedPrimaryHomePhoneNumber.IsIn(chunk))
                .ListAsync(cancellationToken));
        }

        foreach (var match in cellPhoneMatches)
        {
            if (!string.IsNullOrWhiteSpace(match.NormalizedPrimaryCellPhoneNumber))
            {
                existingPhoneNumbers.Add(match.NormalizedPrimaryCellPhoneNumber);
            }
        }

        foreach (var match in homePhoneMatches)
        {
            if (!string.IsNullOrWhiteSpace(match.NormalizedPrimaryHomePhoneNumber))
            {
                existingPhoneNumbers.Add(match.NormalizedPrimaryHomePhoneNumber);
            }
        }

        if (existingPhoneNumbers.Count == normalizedPhoneNumbers.Length)
        {
            return existingPhoneNumbers;
        }

        AddLegacyMatches(existingPhoneNumbers, cellPhoneMatches, normalizedPhoneNumbers, static match => match.PrimaryCellPhoneNumber);
        AddLegacyMatches(existingPhoneNumbers, homePhoneMatches, normalizedPhoneNumbers, static match => match.PrimaryHomePhoneNumber);

        return existingPhoneNumbers;
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string[]>> GetPhoneNumberOwnersAsync(
        IReadOnlyCollection<string> phoneNumberKeys,
        Func<OmnichannelContactIndex, bool> include,
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
        var seenDocuments = new HashSet<long>();

        // One query per column; see LookupBatchSize. A record found by more than one column is counted once.
        foreach (var chunk in ToLookupBatches(keys))
        {
            var matches = new List<OmnichannelContactIndex>();

            matches.AddRange(await _session.QueryIndex<OmnichannelContactIndex>(index =>
                    index.Published && index.NormalizedPrimaryCellPhoneNumber.IsIn(chunk))
                .ListAsync(cancellationToken));
            matches.AddRange(await _session.QueryIndex<OmnichannelContactIndex>(index =>
                    index.Published && index.NormalizedPrimaryHomePhoneNumber.IsIn(chunk))
                .ListAsync(cancellationToken));
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
