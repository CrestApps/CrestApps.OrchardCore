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
    // SQL Server refuses a command with more than 2,100 parameters, and every value in an IN list is one. A lookup
    // names its keys once for each of the indexed number columns it compares, so a command binds the keys of one
    // chunk that many times, plus the published flag. However large an import batch is, its keys are split into
    // chunks that stay inside the limit.
    internal const int MaxParametersPerCommand = 2_000;

    internal const int ComparedNumberColumns = 4;

    internal const int LookupChunkSize = MaxParametersPerCommand / ComparedNumberColumns;

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

        // Each query compares one column, so a chunk may name as many numbers as one command binds.
        foreach (var chunk in normalizedPhoneNumbers.Chunk(MaxParametersPerCommand))
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

        // One query per chunk; see MaxParametersPerCommand.
        foreach (var chunk in keys.Chunk(LookupChunkSize))
        {
            var matches = await _session.QueryIndex<OmnichannelContactIndex>(index =>
                    index.Published &&
                    (index.NormalizedPrimaryCellPhoneNumber.IsIn(chunk) ||
                    index.NormalizedPrimaryHomePhoneNumber.IsIn(chunk) ||
                    index.PrimaryCellPhoneNumber.IsIn(chunk) ||
                    index.PrimaryHomePhoneNumber.IsIn(chunk)))
                .ListAsync(cancellationToken);

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
