#nullable enable

using System.Data;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.DncRegistry;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Filters import rows for omnichannel contacts based on duplicate phone numbers
/// and national do-not-call registry membership.
/// Phone numbers are normalized to E.164 for comparison.
/// </summary>
public sealed class OmnichannelContactImportRowFilter : IContentImportRowFilter
{
    private readonly IEnumerable<INationalDoNotCallRegistry> _registries;
    private readonly IOmnichannelContactDuplicateLookupService _duplicateLookupService;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly ISiteService _siteService;
    private readonly ILogger _logger;
    private bool _ignoreDuplicates;
    private bool _ignoreDoNotCallNumbers;
    private string? _selectedCountryCode;
    private string[] _selectedRegistryKeys = [];
    private Dictionary<string, SeenPhoneOwnerState> _seenPhoneOwners = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string[]> _existingPhoneOwners = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<int, string> _batchSkipReasons = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactImportRowFilter"/> class.
    /// </summary>
    /// <param name="registries">The available do-not-call registries.</param>
    /// <param name="duplicateLookupService">The duplicate lookup service.</param>
    /// <param name="phoneNumberService">The phone number service for E.164 formatting.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="logger">The logger.</param>
    public OmnichannelContactImportRowFilter(
        IEnumerable<INationalDoNotCallRegistry> registries,
        IOmnichannelContactDuplicateLookupService duplicateLookupService,
        IPhoneNumberService phoneNumberService,
        ISiteService siteService,
        ILogger<OmnichannelContactImportRowFilter> logger)
    {
        _registries = registries;
        _duplicateLookupService = duplicateLookupService;
        _phoneNumberService = phoneNumberService;
        _siteService = siteService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<bool> InitializeAsync(ContentImportRowFilterInitContext context)
    {
        var isOmnichannelContact = context.ContentTypeDefinition.Parts?.Any(p =>
            p.PartDefinition.Name == OmnichannelConstants.ContentParts.OmnichannelContact) == true;

        if (!isOmnichannelContact)
        {
            return false;
        }

        var options = context.Entry.GetOrCreate<OmnichannelContactImportOptionsPart>();
        _ignoreDuplicates = options.IgnoreDuplicateByPhoneNumber;
        _ignoreDoNotCallNumbers = options.IgnoreDoNotCallNumbers;
        _selectedCountryCode = NormalizeCountryCode(options.SelectedCountryCode);
        _selectedRegistryKeys = options.SelectedRegistryKeys ?? [];
        _seenPhoneOwners = new Dictionary<string, SeenPhoneOwnerState>(StringComparer.OrdinalIgnoreCase);

        // Apply global enforcement from site settings.
        var site = await _siteService.GetSiteSettingsAsync();
        var settings = site.GetOrCreate<DncRegistrySettings>();

        if (settings.EnforceGlobally)
        {
            _ignoreDoNotCallNumbers = true;
        }

        if (settings.EnforcedRegistryKeys?.Length > 0)
        {
            var mergedKeys = new HashSet<string>(_selectedRegistryKeys, StringComparer.OrdinalIgnoreCase);

            foreach (var key in settings.EnforcedRegistryKeys)
            {
                mergedKeys.Add(key);
            }

            _selectedRegistryKeys = [.. mergedKeys];
        }

        if (!_ignoreDuplicates && !_ignoreDoNotCallNumbers)
        {
            return false;
        }

        if (_ignoreDuplicates)
        {
            _existingPhoneOwners = await _duplicateLookupService.GetAllExistingNormalizedPhoneNumberOwnersAsync(CancellationToken.None);
        }

        return true;
    }

    /// <inheritdoc/>
    public Task PrepareBatchAsync(ContentImportRowFilterBatchContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Rows);

        _batchSkipReasons = [];

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task<bool> ShouldSkipRowAsync(ContentImportRowFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_batchSkipReasons.TryGetValue(context.RowIndex, out var skipReason))
        {
            context.SkipReason = skipReason;

            return true;
        }

        var phoneEntries = ExtractPhoneEntries(context.Row, context.Columns);

        if (phoneEntries.Count == 0)
        {
            return false;
        }

        if (_ignoreDuplicates)
        {
            var contentItemId = GetContentItemId(context.Row, context.Columns);

            foreach (var entry in phoneEntries)
            {
                if (HasConflictingExistingOwner(entry, contentItemId))
                {
                    context.SkipReason = $"{entry.Label} '{entry.RawValue}' already exists in the database.";

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(
                            "Skipping row {RowIndex}: {Reason}",
                            context.RowIndex,
                            context.SkipReason);
                    }

                    return true;
                }

                if (HasConflictingSeenOwner(entry.NormalizedNumber, contentItemId))
                {
                    context.SkipReason = $"{entry.Label} '{entry.RawValue}' already appeared earlier in the import file.";

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(
                            "Skipping row {RowIndex}: {Reason}",
                            context.RowIndex,
                            context.SkipReason);
                    }

                    return true;
                }

                MarkSeenOwner(entry.NormalizedNumber, contentItemId);
            }
        }

        if (_ignoreDoNotCallNumbers)
        {
            var doNotCallNumbers = await LoadDoNotCallNumbersAsync(phoneEntries, CancellationToken.None);

            foreach (var entry in phoneEntries)
            {
                if (doNotCallNumbers.Contains(entry.NormalizedNumber))
                {
                    context.SkipReason = $"{entry.Label} '{entry.RawValue}' is registered on a national do-not-call registry.";

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(
                            "Skipping row {RowIndex}: {Reason}",
                            context.RowIndex,
                            context.SkipReason);
                    }

                    return true;
                }
            }
        }

        return false;
    }

    private List<PhoneEntry> ExtractPhoneEntries(DataRow row, DataColumnCollection columns)
    {
        var entries = new List<PhoneEntry>();

        foreach (DataColumn column in columns)
        {
            var phoneType = GetPhoneType(column.ColumnName);

            if (phoneType == null)
            {
                continue;
            }

            var value = row[column]?.ToString()?.Trim();

            if (!string.IsNullOrEmpty(value))
            {
                var normalizedPhoneNumber = NormalizePhoneNumber(value);

                if (!string.IsNullOrEmpty(normalizedPhoneNumber))
                {
                    entries.Add(new PhoneEntry(normalizedPhoneNumber, value, phoneType, GetComparisonKeys(value, normalizedPhoneNumber)));
                }
            }
        }

        return entries;
    }

    private static string? GetPhoneType(string columnName)
    {
        if (string.Equals(columnName, $"{OmnichannelConstants.NamedParts.ContactMethods}_CellPhone", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(columnName, "CellPhone", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(columnName, "Cell Phone", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(columnName, "Cell", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(columnName, "Mobile", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(columnName, "MobilePhone", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(columnName, "Mobile Phone", StringComparison.OrdinalIgnoreCase))
        {
            return "Cell phone number";
        }

        if (string.Equals(columnName, $"{OmnichannelConstants.NamedParts.ContactMethods}_HomePhone", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(columnName, "HomePhone", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(columnName, "Home Phone", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(columnName, "Phone", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(columnName, "PhoneNumber", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(columnName, "Phone Number", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(columnName, "Landline", StringComparison.OrdinalIgnoreCase))
        {
            return "Home phone number";
        }

        return null;
    }

    private async Task<HashSet<string>> LoadDoNotCallNumbersAsync(
        IEnumerable<PhoneEntry> phoneEntries,
        CancellationToken cancellationToken)
    {
        var allDncNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lookupPhoneNumbers = phoneEntries
            .Select(entry => entry.NormalizedNumber)
            .Where(number => !string.IsNullOrWhiteSpace(number))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var selectedRegistries = _registries
            .Where(r => _selectedRegistryKeys.Contains(r.Key, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (selectedRegistries.Count == 0 || lookupPhoneNumbers.Length == 0)
        {
            return allDncNumbers;
        }

        var searchContext = new NumberSearchContext
        {
            CountryCode = _selectedCountryCode,
        };
        var tasks = selectedRegistries.Select(registry =>
            QueryRegistrySafeAsync(registry, lookupPhoneNumbers, searchContext, cancellationToken));

        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            foreach (var number in result)
            {
                var normalizedNumber = NormalizePhoneNumber(number);

                if (!string.IsNullOrEmpty(normalizedNumber))
                {
                    allDncNumbers.Add(normalizedNumber);
                }
            }
        }

        return allDncNumbers;
    }

    private async Task<HashSet<string>> QueryRegistrySafeAsync(
        INationalDoNotCallRegistry registry,
        IEnumerable<string> phoneNumbers,
        NumberSearchContext searchContext,
        CancellationToken cancellationToken)
    {
        try
        {
            return await registry.GetRegisteredNumbersAsync(phoneNumbers, searchContext, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error loading do-not-call numbers from registry {RegistryKey}.",
                registry.Key);

            return [];
        }
    }

    private string NormalizePhoneNumber(string phoneNumber)
    {
        if (_phoneNumberService.TryFormatToE164(phoneNumber, GetFormattingRegionCode(phoneNumber), out var e164))
        {
            return e164;
        }

        // Fallback: strip non-digits for consistent comparison of national-format numbers.
        return new string(phoneNumber.Where(char.IsDigit).ToArray());
    }

    private string? GetFormattingRegionCode(string phoneNumber)
        => !string.IsNullOrWhiteSpace(phoneNumber) && phoneNumber.TrimStart().StartsWith('+')
            ? null
            : _selectedCountryCode;

    private bool HasConflictingExistingOwner(PhoneEntry entry, string? contentItemId)
    {
        foreach (var key in entry.ComparisonKeys)
        {
            if (!_existingPhoneOwners.TryGetValue(key, out var owners) || owners.Length == 0)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(contentItemId))
            {
                return true;
            }

            if (owners.Any(owner => !string.Equals(owner, contentItemId, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private bool HasConflictingSeenOwner(string normalizedPhoneNumber, string? contentItemId)
    {
        if (!_seenPhoneOwners.TryGetValue(normalizedPhoneNumber, out var seenState))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(contentItemId))
        {
            return true;
        }

        return seenState.HasAnonymousRows ||
            seenState.ContentItemIds.Any(owner => !string.Equals(owner, contentItemId, StringComparison.OrdinalIgnoreCase));
    }

    private void MarkSeenOwner(string normalizedPhoneNumber, string? contentItemId)
    {
        if (!_seenPhoneOwners.TryGetValue(normalizedPhoneNumber, out var seenState))
        {
            seenState = new SeenPhoneOwnerState();
            _seenPhoneOwners[normalizedPhoneNumber] = seenState;
        }

        if (string.IsNullOrWhiteSpace(contentItemId))
        {
            seenState.HasAnonymousRows = true;
            return;
        }

        seenState.ContentItemIds.Add(contentItemId);
    }

    private static string? GetContentItemId(DataRow row, DataColumnCollection columns)
    {
        foreach (DataColumn column in columns)
        {
            if (!string.Equals(column.ColumnName, nameof(ContentItem.ContentItemId), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return row[column]?.ToString()?.Trim();
        }

        return null;
    }

    private static string? NormalizeCountryCode(string? countryCode)
        => string.IsNullOrWhiteSpace(countryCode)
            ? null
            : countryCode.Trim().ToUpperInvariant();

    private static string[] GetComparisonKeys(string rawValue, string normalizedNumber)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(normalizedNumber))
        {
            keys.Add(normalizedNumber);
        }

        var digits = new string(rawValue.Where(char.IsDigit).ToArray());

        if (!string.IsNullOrWhiteSpace(digits))
        {
            keys.Add(digits);
        }

        var trimmedValue = rawValue.Trim();

        if (!string.IsNullOrWhiteSpace(trimmedValue))
        {
            keys.Add(trimmedValue);
        }

        return [.. keys];
    }

    private sealed class SeenPhoneOwnerState
    {
        public HashSet<string> ContentItemIds { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool HasAnonymousRows { get; set; }
    }

    private sealed record PhoneEntry(string NormalizedNumber, string RawValue, string Label, string[] ComparisonKeys);
}
