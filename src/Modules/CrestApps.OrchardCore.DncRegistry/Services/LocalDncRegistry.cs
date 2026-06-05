using CrestApps.OrchardCore.DncRegistry.Indexes;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.Extensions.Localization;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.DncRegistry.Services;

/// <summary>
/// A local do-not-call registry that checks phone numbers against
/// administrator-uploaded CSV lists stored in YesSql.
/// Supports filtering by country via <see cref="NumberSearchContext"/>.
/// Phone numbers are expected in E.164 format for comparison.
/// </summary>
public sealed class LocalDncRegistry : INationalDoNotCallRegistry
{
    private readonly ISession _session;
    private readonly IPhoneNumberService _phoneNumberService;

    /// <summary>
    /// Gets the unique key identifying this registry.
    /// </summary>
    public string Key => "local-dnc";

    /// <summary>
    /// Gets the localized display name of this registry.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the localized description of this registry.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalDncRegistry"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="phoneNumberService">The phone number service for E.164 formatting.</param>
    /// <param name="S">The string localizer.</param>
    public LocalDncRegistry(
        ISession session,
        IPhoneNumberService phoneNumberService,
        IStringLocalizer<LocalDncRegistry> S)
    {
        _session = session;
        _phoneNumberService = phoneNumberService;

        DisplayName = S["Local Do Not Call Registry"];
        Description = S["Checks phone numbers against locally uploaded CSV lists organized by country."];
    }

    /// <inheritdoc/>
    public Task<HashSet<string>> GetRegisteredNumbersAsync(
        IEnumerable<string> phoneNumbers,
        CancellationToken cancellationToken = default)
        => GetRegisteredNumbersAsync(phoneNumbers, context: null, cancellationToken);

    /// <inheritdoc/>
    public async Task<HashSet<string>> GetRegisteredNumbersAsync(
        IEnumerable<string> phoneNumbers,
        NumberSearchContext context,
        CancellationToken cancellationToken = default)
    {
        var dncNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Normalize input numbers to E.164 for comparison.
        var e164Map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var phone in phoneNumbers)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                continue;
            }

            var e164 = NormalizeToE164(phone, context?.CountryCode);

            if (!string.IsNullOrEmpty(e164) && !e164Map.ContainsKey(e164))
            {
                e164Map[e164] = phone;
            }
        }

        if (e164Map.Count == 0)
        {
            return dncNumbers;
        }

        var e164Numbers = e164Map.Keys.ToList();

        IQuery<LocalDncEntry> query;

        if (!string.IsNullOrWhiteSpace(context?.CountryCode))
        {
            var upperCountry = context.CountryCode.ToUpperInvariant();

            query = _session.Query<LocalDncEntry, LocalDncEntryIndex>(
                i => i.PhoneNumber.IsIn(e164Numbers) &&
                     i.CountryCode == upperCountry,
                collection: DncRegistryConstants.CollectionName);
        }
        else
        {
            query = _session.Query<LocalDncEntry, LocalDncEntryIndex>(
                i => i.PhoneNumber.IsIn(e164Numbers),
                collection: DncRegistryConstants.CollectionName);
        }

        var entries = await query.ListAsync(cancellationToken);
        var listIds = entries
            .Select(entry => entry.ListId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (listIds.Length == 0)
        {
            return dncNumbers;
        }

        var completedListIds = (await _session.Query<LocalDncList, LocalDncListIndex>(
                i => i.ListId.IsIn(listIds) && i.Status == LocalDncListStatus.Completed,
                collection: DncRegistryConstants.CollectionName)
            .ListAsync(cancellationToken))
            .Select(list => list.ListId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (!completedListIds.Contains(entry.ListId))
            {
                continue;
            }

            // Return the original input number that matched.
            if (e164Map.TryGetValue(entry.PhoneNumber, out var originalNumber))
            {
                dncNumbers.Add(originalNumber);
            }
        }

        return dncNumbers;
    }

    private string NormalizeToE164(string phoneNumber, string regionCode)
    {
        if (_phoneNumberService.TryFormatToE164(phoneNumber, regionCode, out var e164))
        {
            return e164;
        }

        return null;
    }
}
