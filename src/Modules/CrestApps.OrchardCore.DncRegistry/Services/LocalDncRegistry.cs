using CrestApps.OrchardCore.DncRegistry.Indexes;
using CrestApps.OrchardCore.DncRegistry.Models;
using Microsoft.Extensions.Localization;
using YesSql;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.DncRegistry.Services;

/// <summary>
/// A local do-not-call registry that checks phone numbers against
/// administrator-uploaded CSV lists stored in YesSql.
/// Supports filtering by country via <see cref="NumberSearchContext"/>.
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
        var normalizedNumbers = phoneNumbers
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(NormalizePhoneNumber)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedNumbers.Count == 0)
        {
            return dncNumbers;
        }

        IQuery<LocalDncEntry> query;

        if (!string.IsNullOrWhiteSpace(context?.CountryCode))
        {
            var upperCountry = context.CountryCode.ToUpperInvariant();

            query = _session.Query<LocalDncEntry, LocalDncEntryIndex>(
                i => i.PhoneNumber.IsIn(normalizedNumbers) &&
                     i.CountryCode == upperCountry,
                collection: DncRegistryConstants.CollectionName);
        }
        else
        {
            query = _session.Query<LocalDncEntry, LocalDncEntryIndex>(
                i => i.PhoneNumber.IsIn(normalizedNumbers),
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

            foreach (var original in phoneNumbers)
            {
                if (string.Equals(NormalizePhoneNumber(original), entry.PhoneNumber, StringComparison.OrdinalIgnoreCase))
                {
                    dncNumbers.Add(original);
                }
            }
        }

        return dncNumbers;
    }

    private static string NormalizePhoneNumber(string phoneNumber)
        => new(phoneNumber.Where(char.IsDigit).ToArray());
}
