#nullable enable

using System.Data;
using CrestApps.OrchardCore.ContentTransfer;
using CrestApps.OrchardCore.DncRegistry;
using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

/// <summary>
/// Filters import rows for omnichannel contacts based on duplicate phone numbers
/// and national do-not-call registry membership.
/// </summary>
public sealed class OmnichannelContactImportRowFilter : IContentImportRowFilter
{
    private readonly IEnumerable<INationalDoNotCallRegistry> _registries;
    private readonly ISiteService _siteService;
    private readonly ILogger _logger;
    private bool _ignoreDuplicates;
    private bool _ignoreDoNotCallNumbers;
    private string[] _selectedRegistryKeys = [];
    private string[] _phoneColumnNames = [];
    private HashSet<string> _seenPhoneNumbers = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelContactImportRowFilter"/> class.
    /// </summary>
    /// <param name="registries">The available do-not-call registries.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="logger">The logger.</param>
    public OmnichannelContactImportRowFilter(
        IEnumerable<INationalDoNotCallRegistry> registries,
        ISiteService siteService,
        ILogger<OmnichannelContactImportRowFilter> logger)
    {
        _registries = registries;
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

        _phoneColumnNames =
        [
            $"{OmnichannelConstants.NamedParts.ContactMethods}_CellPhone",
            "CellPhone", "Cell Phone", "Cell", "Mobile", "MobilePhone", "Mobile Phone",
            $"{OmnichannelConstants.NamedParts.ContactMethods}_HomePhone",
            "HomePhone", "Home Phone", "Phone", "PhoneNumber", "Phone Number", "Landline",
        ];

        return true;
    }

    /// <inheritdoc/>
    public async Task<bool> ShouldSkipRowAsync(ContentImportRowFilterContext context)
    {
        var phoneNumbers = ExtractPhoneNumbers(context.Row, context.Columns);

        if (phoneNumbers.Count == 0)
        {
            return false;
        }

        if (_ignoreDuplicates)
        {
            var isDuplicate = true;

            foreach (var phone in phoneNumbers)
            {
                if (_seenPhoneNumbers.Add(phone))
                {
                    isDuplicate = false;
                }
            }

            if (isDuplicate)
            {
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        "Skipping row {RowIndex} due to duplicate phone number.",
                        context.RowIndex);
                }

                return true;
            }
        }

        if (_ignoreDoNotCallNumbers)
        {
            var doNotCallNumbers = await LoadDoNotCallNumbersAsync(phoneNumbers, CancellationToken.None);

            foreach (var phone in phoneNumbers)
            {
                var normalized = NormalizePhoneNumber(phone);

                if (doNotCallNumbers.Contains(normalized))
                {
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        _logger.LogDebug(
                            "Skipping row {RowIndex} because phone number is on the do-not-call list.",
                            context.RowIndex);
                    }

                    return true;
                }
            }
        }

        return false;
    }

    private List<string> ExtractPhoneNumbers(DataRow row, DataColumnCollection columns)
    {
        var phoneNumbers = new List<string>();

        foreach (DataColumn column in columns)
        {
            if (!IsPhoneColumn(column.ColumnName))
            {
                continue;
            }

            var value = row[column]?.ToString()?.Trim();

            if (!string.IsNullOrEmpty(value))
            {
                phoneNumbers.Add(value);
            }
        }

        return phoneNumbers;
    }

    private bool IsPhoneColumn(string columnName)
    {
        foreach (var name in _phoneColumnNames)
        {
            if (string.Equals(columnName, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<HashSet<string>> LoadDoNotCallNumbersAsync(
        List<string> phoneNumbers,
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

    private static string NormalizePhoneNumber(string phoneNumber)
        => new string(phoneNumber.Where(char.IsDigit).ToArray());
}
