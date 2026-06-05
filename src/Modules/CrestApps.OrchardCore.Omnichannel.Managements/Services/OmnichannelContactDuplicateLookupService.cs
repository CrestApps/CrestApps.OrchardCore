using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.PhoneNumbers;
using OrchardCore.ContentManagement.Records;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

/// <summary>
/// Queries stored omnichannel contacts for existing normalized phone numbers.
/// Phone numbers are compared in E.164 format.
/// </summary>
public sealed class OmnichannelContactDuplicateLookupService : IOmnichannelContactDuplicateLookupService
{
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
        var cellPhoneMatches = await _session.QueryIndex<OmnichannelContactIndex>(index =>
                index.NormalizedPrimaryCellPhoneNumber.IsIn(normalizedPhoneNumbers))
            .ListAsync(cancellationToken);
        var homePhoneMatches = await _session.QueryIndex<OmnichannelContactIndex>(index =>
                index.NormalizedPrimaryHomePhoneNumber.IsIn(normalizedPhoneNumbers))
            .ListAsync(cancellationToken);

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
    public async Task<HashSet<string>> GetAllExistingNormalizedPhoneNumbersAsync(CancellationToken cancellationToken)
    {
        var existingPhoneNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var allIndexes = await _session.QueryIndex<OmnichannelContactIndex>()
            .ListAsync(cancellationToken);

        if (!allIndexes.Any())
        {
            return existingPhoneNumbers;
        }

        var contentItemIds = allIndexes
            .Select(i => i.ContentItemId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (contentItemIds.Length == 0)
        {
            return existingPhoneNumbers;
        }

        // Only include phone numbers from content items that are still alive (Latest).
        var latestContentItemIds = (await _session.QueryIndex<ContentItemIndex>(i =>
                i.ContentItemId.IsIn(contentItemIds) && i.Latest)
            .ListAsync(cancellationToken))
            .Select(i => i.ContentItemId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var index in allIndexes)
        {
            if (!latestContentItemIds.Contains(index.ContentItemId))
            {
                continue;
            }

            AddPhoneFromIndex(existingPhoneNumbers, index.NormalizedPrimaryCellPhoneNumber, index.PrimaryCellPhoneNumber);
            AddPhoneFromIndex(existingPhoneNumbers, index.NormalizedPrimaryHomePhoneNumber, index.PrimaryHomePhoneNumber);
        }

        return existingPhoneNumbers;
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

        if (_phoneNumberService.TryFormatToE164(phoneNumber, null, out var e164))
        {
            return e164;
        }

        // Fallback: strip non-digits for consistent comparison of national-format numbers.
        return new string(phoneNumber.Where(char.IsDigit).ToArray());
    }
}
