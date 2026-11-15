using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Conditions;
using CrestApps.OrchardCore.ContentFields.Conditions;
using CrestApps.OrchardCore.ContentFields.Settings;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentFields.ConditionalFields;

/// <summary>
/// Works out which fields of an item are hidden: those whose rule (<see cref="ConditionalFieldSettings"/>) isn't met,
/// a field under a hidden one, and those an <see cref="IContentFieldVisibilityProvider"/> hides. Fields are keyed
/// <c>{TypePartName}.{FieldName}</c>.
/// </summary>
internal sealed class ConditionalFieldsService
{
    private readonly Dictionary<string, IContentFieldValueReader> _readers;
    private readonly IEnumerable<IContentConditionSource> _sources;
    private readonly IEnumerable<IContentFieldVisibilityProvider> _visibilityProviders;

    public ConditionalFieldsService(
        IEnumerable<IContentFieldValueReader> readers,
        IEnumerable<IContentConditionSource> sources,
        IEnumerable<IContentFieldVisibilityProvider> visibilityProviders)
    {
        // The last reader registered for a field type wins, so a module can replace a built-in one.
        _readers = new Dictionary<string, IContentFieldValueReader>(StringComparer.Ordinal);

        foreach (var reader in readers)
        {
            _readers[reader.FieldType] = reader;
        }

        _sources = sources;
        _visibilityProviders = visibilityProviders;
    }

    /// <summary>
    /// Gets whether something besides field rules may hide fields (a visibility provider is registered).
    /// </summary>
    public bool HasVisibilityProviders => _visibilityProviders.Any();

    /// <summary>
    /// Gets the fields of a type, with their keys.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The fields.</returns>
    public static IEnumerable<(string Key, ContentTypePartDefinition Part, ContentPartFieldDefinition Field)> GetFields(ContentTypeDefinition type)
    {
        foreach (var part in type?.Parts ?? [])
        {
            foreach (var field in part.PartDefinition?.Fields ?? [])
            {
                yield return ($"{part.Name}.{field.Name}", part, field);
            }
        }
    }

    /// <summary>
    /// Gets the rules of a type's fields.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The rules, by field key; fields without one are left out.</returns>
    public static Dictionary<string, ConditionRule> GetRules(ContentTypeDefinition type)
    {
        var rules = new Dictionary<string, ConditionRule>(StringComparer.Ordinal);

        foreach (var (key, _, field) in GetFields(type))
        {
            var rule = field.GetSettings<ConditionalFieldSettings>().ShowWhen;

            if (!ConditionRule.IsEmpty(rule))
            {
                rules[key] = rule;
            }
        }

        return rules;
    }

    /// <summary>
    /// Gets the hidden fields of an item.
    /// </summary>
    /// <param name="contentItem">The item, as the editor has it.</param>
    /// <param name="type">Its type.</param>
    /// <returns>The keys of the hidden fields.</returns>
    public async Task<IReadOnlySet<string>> GetHiddenAsync(ContentItem contentItem, ContentTypeDefinition type)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        var hidden = new HashSet<string>(StringComparer.Ordinal);

        if (type is null)
        {
            return hidden;
        }

        var rules = GetRules(type);

        if (rules.Count > 0)
        {
            var values = await ReadSourcesAsync(contentItem, type, rules.Values.SelectMany(ConditionalVisibility.Sources));

            hidden.UnionWith(ConditionalVisibility.GetHidden(rules, source => values.GetValueOrDefault(source)));
        }

        foreach (var provider in _visibilityProviders)
        {
            hidden.UnionWith(await provider.GetHiddenFieldsAsync(contentItem, type) ?? []);
        }

        return hidden;
    }

    /// <summary>
    /// Describes every source a field rule of a type can read: its other fields that have a reader, and the values
    /// modules provide.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The sources, fields first, in the type's order.</returns>
    public async Task<List<ContentConditionSourceDescriptor>> DescribeSourcesAsync(ContentTypeDefinition type)
    {
        var descriptors = new List<ContentConditionSourceDescriptor>();

        foreach (var (key, part, field) in GetFields(type))
        {
            if (!_readers.TryGetValue(field.FieldDefinition.Name, out var reader))
            {
                continue;
            }

            var descriptor = new ContentConditionSourceDescriptor
            {
                Name = key,
                DisplayName = type.Parts.Count(item => item.PartDefinition.Fields.Any()) > 1
                    ? $"{part.DisplayName()} > {field.DisplayName()}"
                    : field.DisplayName(),
                Kind = field.FieldDefinition.Name,
                InputName = key,
            };

            await reader.DescribeAsync(field, descriptor);
            descriptors.Add(descriptor);
        }

        foreach (var source in _sources)
        {
            foreach (var descriptor in await source.DescribeAsync(type) ?? [])
            {
                if (!string.IsNullOrEmpty(descriptor?.Name) && !descriptor.Name.Contains('.'))
                {
                    descriptors.Add(descriptor);
                }
            }
        }

        return descriptors;
    }

    /// <summary>
    /// Reads the sources a set of rules names.
    /// </summary>
    /// <param name="contentItem">The item.</param>
    /// <param name="type">Its type.</param>
    /// <param name="names">The sources.</param>
    /// <returns>The values of the sources that are known; an unknown source is left out.</returns>
    public async Task<Dictionary<string, ConditionValue>> ReadSourcesAsync(ContentItem contentItem, ContentTypeDefinition type, IEnumerable<string> names)
    {
        var values = new Dictionary<string, ConditionValue>(StringComparer.Ordinal);

        foreach (var name in names.Distinct(StringComparer.Ordinal))
        {
            if (await ReadSourceAsync(contentItem, type, name) is { } value)
            {
                values[name] = value;
            }
        }

        return values;
    }

    private async Task<ConditionValue> ReadSourceAsync(ContentItem contentItem, ContentTypeDefinition type, string name)
    {
        var dot = name.IndexOf('.');

        if (dot > 0)
        {
            var partName = name[..dot];
            var fieldName = name[(dot + 1)..];
            var part = type.Parts.FirstOrDefault(item => item.Name == partName);
            var field = part?.PartDefinition.Fields.FirstOrDefault(item => item.Name == fieldName);

            if (field is null || !_readers.TryGetValue(field.FieldDefinition.Name, out var reader))
            {
                return null;
            }

            var stored = ((JsonObject)contentItem.Content)[partName]?[fieldName];

            return await reader.ReadAsync(stored, field) ?? ConditionValue.Empty;
        }

        foreach (var source in _sources)
        {
            if (await source.ReadAsync(name, contentItem) is { } value)
            {
                return value;
            }
        }

        return null;
    }
}
