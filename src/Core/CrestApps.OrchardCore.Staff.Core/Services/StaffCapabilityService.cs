using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Data.Documents;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Staff.Core.Services;

/// <summary>
/// The admin's capability records: the ones they added and their changes to built-in ones, kept in one document.
/// </summary>
public sealed class StaffCapabilitiesDocument : Document
{
    /// <summary>
    /// Gets or sets the records.
    /// </summary>
    public List<StaffCapabilityDefinition> Capabilities { get; set; } = [];
}

/// <summary>
/// The default <see cref="IStaffCapabilityService"/>: the modules' capabilities (<see cref="StaffOptions"/>) with the
/// admin's changes from <see cref="StaffCapabilitiesDocument"/>, then the ones admins added.
/// </summary>
internal sealed partial class StaffCapabilityService : IStaffCapabilityService
{
    private readonly IDocumentManager<StaffCapabilitiesDocument> _documentManager;
    private readonly StaffOptions _options;
    private readonly IStringLocalizerFactory _localizerFactory;

    internal readonly IStringLocalizer S;

    public StaffCapabilityService(
        IDocumentManager<StaffCapabilitiesDocument> documentManager,
        IOptions<StaffOptions> options,
        IStringLocalizerFactory localizerFactory,
        IStringLocalizer<StaffCapabilityService> stringLocalizer)
    {
        _documentManager = documentManager;
        _options = options.Value;
        _localizerFactory = localizerFactory;
        S = stringLocalizer;
    }

    public async Task<IReadOnlyList<StaffCapabilityInfo>> ListAsync(bool includeDisabled = false, CancellationToken cancellationToken = default)
    {
        var records = (await _documentManager.GetOrCreateImmutableAsync()).Capabilities
            .Where(record => !string.IsNullOrEmpty(record.Name))
            .GroupBy(record => record.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
        var all = new List<StaffCapabilityInfo>();

        foreach (var builtIn in _options.Capabilities.Values)
        {
            records.TryGetValue(builtIn.Name, out var record);
            all.Add(new StaffCapabilityInfo
            {
                Name = builtIn.Name,
                DisplayName = !string.IsNullOrWhiteSpace(record?.DisplayName) ? record.DisplayName : builtIn.DisplayName is null ? builtIn.Name : _localizerFactory.Localize(builtIn.DisplayName).Value,
                Description = record is not null ? record.Description : builtIn.Description is null ? null : _localizerFactory.Localize(builtIn.Description).Value,
                Position = record?.Position ?? builtIn.Position,
                Enabled = record?.Enabled ?? true,
                IsBuiltIn = true,
                IsCustomized = record is not null,
            });
        }

        foreach (var record in records.Values.Where(record => !_options.Capabilities.ContainsKey(record.Name)))
        {
            all.Add(new StaffCapabilityInfo
            {
                Name = record.Name,
                DisplayName = string.IsNullOrWhiteSpace(record.DisplayName) ? record.Name : record.DisplayName,
                Description = record.Description,
                Position = record.Position,
                Enabled = record.Enabled,
            });
        }

        return [.. all
            .Where(capability => includeDisabled || capability.Enabled)
            .OrderBy(capability => capability.Position)
            .ThenBy(capability => capability.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
    }

    public async Task<StaffCapabilityInfo> FindAsync(string name, CancellationToken cancellationToken = default)
        => string.IsNullOrWhiteSpace(name)
            ? null
            : (await ListAsync(includeDisabled: true, cancellationToken)).FirstOrDefault(capability => string.Equals(capability.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

    public async Task<IReadOnlyList<string>> SaveAsync(StaffCapabilityDefinition definition, bool isNew, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var errors = new List<string>();
        var name = definition.Name?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            errors.Add(S["The key is required."]);
        }
        else if (!KeyPattern().IsMatch(name))
        {
            errors.Add(S["The key can only have letters, digits, dots, dashes and underscores, and starts with a letter."]);
        }

        if (string.IsNullOrWhiteSpace(definition.DisplayName))
        {
            errors.Add(S["The name is required."]);
        }

        var existing = string.IsNullOrEmpty(name) ? null : await FindAsync(name, cancellationToken);

        if (isNew && existing is not null)
        {
            errors.Add(S["A capability with the key '{0}' already exists.", name]);
        }
        else if (!isNew && existing is null)
        {
            errors.Add(S["The capability '{0}' doesn't exist.", name]);
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var document = await _documentManager.GetOrCreateMutableAsync();

        document.Capabilities.RemoveAll(record => string.Equals(record.Name, name, StringComparison.OrdinalIgnoreCase));
        document.Capabilities.Add(new StaffCapabilityDefinition
        {
            Name = existing?.Name ?? name,
            DisplayName = definition.DisplayName.Trim(),
            Description = string.IsNullOrWhiteSpace(definition.Description) ? null : definition.Description.Trim(),
            Position = definition.Position,
            Enabled = definition.Enabled,
        });

        await _documentManager.UpdateAsync(document);

        return [];
    }

    public async Task<bool> DeleteAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        // A built-in capability loses only the admin's changes: its module still relies on it.
        var document = await _documentManager.GetOrCreateMutableAsync();
        var removed = document.Capabilities.RemoveAll(record => string.Equals(record.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

        if (removed == 0)
        {
            return false;
        }

        await _documentManager.UpdateAsync(document);

        return true;
    }

    public async Task<IReadOnlyList<StaffCapabilityDefinition>> GetDefinitionsAsync(CancellationToken cancellationToken = default)
        => [.. (await _documentManager.GetOrCreateImmutableAsync()).Capabilities];

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9._-]{0,63}$")]
    private static partial Regex KeyPattern();
}
