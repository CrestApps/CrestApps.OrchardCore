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
    private string[] _selectedRegistryKeys = [];
    private HashSet<string> _seenPhoneNumbers = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _existingPhoneNumbers = new(StringComparer.OrdinalIgnoreCase);
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
        _selectedRegistryKeys = options.SelectedRegistryKeys ?? [];
        _seenPhoneNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
            _existingPhoneNumbers = await _duplicateLookupService.GetAllExistingNormalizedPhoneNumbersAsync(CancellationToken.None);
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
            foreach (var entry in phoneEntries)
            {
                if (_existingPhoneNumbers.Contains(entry.NormalizedNumber))
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

                if (!_seenPhoneNumbers.Add(entry.NormalizedNumber))
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
            }
        }

        if (_ignoreDoNotCallNumbers)
        {
            var normalizedNumbers = phoneEntries.Select(e => e.NormalizedNumber);
            var doNotCallNumbers = await LoadDoNotCallNumbersAsync(normalizedNumbers, CancellationToken.None);

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
                    entries.Add(new PhoneEntry(normalizedPhoneNumber, value, phoneType));
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
        IEnumerable<string> phoneNumbers,
        CancellationToken cancellationToken)
    {
        var allDncNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var selectedRegistries = _registries
            .Where(r => _selectedRegistryKeys.Contains(r.Key, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (selectedRegistries.Count == 0)
        {
            return allDncNumbers;
        }

        var tasks = selectedRegistries.Select(registry =>
            QueryRegistrySafeAsync(registry, phoneNumbers, cancellationToken));

        var results = await Task.WhenAll(tasks);

        foreach (var result in results)
        {
            foreach (var number in result)
            {
                allDncNumbers.Add(NormalizePhoneNumber(number));
            }
        }

        return allDncNumbers;
    }

    private async Task<HashSet<string>> QueryRegistrySafeAsync(
        INationalDoNotCallRegistry registry,
        IEnumerable<string> phoneNumbers,
        CancellationToken cancellationToken)
    {
        try
        {
            return await registry.GetRegisteredNumbersAsync(phoneNumbers, cancellationToken);
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
        if (_phoneNumberService.TryFormatToE164(phoneNumber, null, out var e164))
        {
            return e164;
        }

        // Fallback: strip non-digits for consistent comparison of national-format numbers.
        return new string(phoneNumber.Where(char.IsDigit).ToArray());
    }

    private sealed record PhoneEntry(string NormalizedNumber, string RawValue, string Label);
}
