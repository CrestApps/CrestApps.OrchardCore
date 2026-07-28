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
    /// <param name="S">The string localizer.</param>
    public LocalDncRegistry(
        ISession session,
        IStringLocalizer<LocalDncRegistry> S)
    {
        _session = session;

        DisplayName = S["Local Do Not Call Registry"];
        Description = S["Checks phone numbers against locally uploaded CSV lists organized by country."];
    }

    /// <inheritdoc/>
    public Task<HashSet<PhoneNumber>> GetRegisteredNumbersAsync(
        IEnumerable<PhoneNumber> phoneNumbers,
        CancellationToken cancellationToken = default)
        => GetRegisteredNumbersAsync(phoneNumbers, context: null, cancellationToken);

    /// <inheritdoc/>
    public async Task<HashSet<PhoneNumber>> GetRegisteredNumbersAsync(
        IEnumerable<PhoneNumber> phoneNumbers,
        NumberSearchContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(phoneNumbers);

        var dncNumbers = new HashSet<PhoneNumber>();

        // The numbers arrive canonical, so the stored entries and the queried values are in the same form by
        // construction. They used to be re-normalized here against whatever country the caller happened to
        // pass, which meant the same national number could be stored under one country and looked up under
        // another and never match.
        var queriedNumbers = new Dictionary<string, PhoneNumber>(StringComparer.Ordinal);

        foreach (var phoneNumber in phoneNumbers)
        {
            if (phoneNumber.HasValue)
            {
                queriedNumbers[phoneNumber.Value] = phoneNumber;
            }
        }

        if (queriedNumbers.Count == 0)
        {
            return dncNumbers;
        }

        var e164Numbers = queriedNumbers.Keys.ToList();

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

            if (TryResolveQueriedNumber(queriedNumbers, entry.PhoneNumber, out var registeredNumber))
            {
                dncNumbers.Add(registeredNumber);
            }
        }

        return dncNumbers;
    }

    /// <summary>
    /// Resolves a stored entry that the query matched back to the number the caller asked about.
    /// </summary>
    /// <param name="queriedNumbers">The numbers the caller asked about, keyed by their canonical value.</param>
    /// <param name="storedNumber">The number as it is stored on the matched entry.</param>
    /// <param name="registeredNumber">When this method returns, the number the caller asked about.</param>
    /// <returns><see langword="true"/> when the stored entry could be resolved; otherwise <see langword="false"/>.</returns>
    internal static bool TryResolveQueriedNumber(
        IReadOnlyDictionary<string, PhoneNumber> queriedNumbers,
        string storedNumber,
        out PhoneNumber registeredNumber)
    {
        // What gets reported as listed is the number that was asked about, never the stored string parsed a
        // second time. Re-parsing would let a stored value the query still matched — a provider that ignores
        // trailing blanks in an IN comparison is enough — fail to parse and be dropped, and a dropped match
        // reads to the dialer as "not listed", which is the exact fail-open this whole change exists to end.
        if (storedNumber is not null && queriedNumbers.TryGetValue(storedNumber, out registeredNumber))
        {
            return true;
        }

        var trimmedStoredNumber = storedNumber?.Trim();

        foreach (var (value, queriedNumber) in queriedNumbers)
        {
            if (string.Equals(value, trimmedStoredNumber, StringComparison.Ordinal))
            {
                registeredNumber = queriedNumber;

                return true;
            }
        }

        registeredNumber = default;

        return false;
    }
}
