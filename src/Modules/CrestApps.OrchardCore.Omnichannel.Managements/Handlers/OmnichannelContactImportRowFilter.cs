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
            HashSet<PhoneNumber> doNotCallNumbers;

            try
            {
                doNotCallNumbers = await LoadDoNotCallNumbersAsync(phoneEntries, CancellationToken.None);
            }
            catch (DoNotCallScreeningException ex)
            {
                context.SkipReason = ex.Message;

                _logger.LogError(
                    ex,
                    "Skipping row {RowIndex}: {Reason}",
                    context.RowIndex,
                    context.SkipReason);

                return true;
            }

            foreach (var entry in phoneEntries)
            {
                // A number that could not be read as a phone number cannot be screened, and importing it
                // anyway would present an unscreened number as one the registries had cleared. The row is
                // skipped with the reason stated so the operator can supply the country the file is in.
                if (!entry.Canonical.HasValue)
                {
                    context.SkipReason = $"{entry.Label} '{entry.RawValue}' could not be read as a phone number, so it could not be checked against a national do-not-call registry.";

                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(
                            "Skipping row {RowIndex}: {Reason}",
                            context.RowIndex,
                            context.SkipReason);
                    }

                    return true;
                }

                if (doNotCallNumbers.Contains(entry.Canonical))
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
                _phoneNumberService.TryParse(value, GetFormattingRegionCode(value), out var canonicalNumber);

                var normalizedPhoneNumber = PhoneNumberComparisonKey.For(canonicalNumber, value);

                if (!string.IsNullOrEmpty(normalizedPhoneNumber))
                {
                    entries.Add(new PhoneEntry(normalizedPhoneNumber, canonicalNumber, value, phoneType, PhoneNumberComparisonKey.AllFor(canonicalNumber, value)));
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

    private async Task<HashSet<PhoneNumber>> LoadDoNotCallNumbersAsync(
        IEnumerable<PhoneEntry> phoneEntries,
        CancellationToken cancellationToken)
    {
        var allDncNumbers = new HashSet<PhoneNumber>();

        // Only canonical numbers are checked. A number the import could not canonicalize used to be sent to
        // the registries as a digits-only string, which no registry could compare, so the row was imported as
        // though the registries had cleared it.
        var lookupPhoneNumbers = phoneEntries
            .Where(entry => entry.Canonical.HasValue)
            .Select(entry => entry.Canonical)
            .Distinct()
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
            QueryRegistryAsync(registry, lookupPhoneNumbers, searchContext, cancellationToken));

        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            allDncNumbers.UnionWith(result);
        }

        return allDncNumbers;
    }

    private async Task<HashSet<PhoneNumber>> QueryRegistryAsync(
        INationalDoNotCallRegistry registry,
        IEnumerable<PhoneNumber> phoneNumbers,
        NumberSearchContext searchContext,
        CancellationToken cancellationToken)
    {
        try
        {
            return await registry.GetRegisteredNumbersAsync(phoneNumbers, searchContext, cancellationToken);
        }
        catch (DoNotCallScreeningException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(
                ex,
                "Error loading do-not-call numbers from registry {RegistryKey}.",
                registry.Key);

            // Returning an empty set here would tell the import that the registry cleared every number it
            // was asked about, and the rows would be imported as callable. The operator asked for these
            // numbers to be screened; a screening that did not happen is not a screening that passed.
            throw new DoNotCallScreeningException(
                registry.Key,
                $"The '{registry.Key}' do-not-call registry could not be reached, so the numbers in this file could not be screened.",
                ex);
        }
    }

    private string? GetFormattingRegionCode(string phoneNumber)
        => PhoneNumber.IsE164(phoneNumber?.Trim())
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


    private sealed class SeenPhoneOwnerState
    {
        public HashSet<string> ContentItemIds { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool HasAnonymousRows { get; set; }
    }

    private sealed record PhoneEntry(string NormalizedNumber, PhoneNumber Canonical, string RawValue, string Label, string[] ComparisonKeys);
}
