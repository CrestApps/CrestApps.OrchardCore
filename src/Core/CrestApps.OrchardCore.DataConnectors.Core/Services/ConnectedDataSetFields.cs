using System.Text;
using CrestApps.OrchardCore.DataConnectors.Models;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.DataConnectors.Services;

/// <summary>
/// Builds the fields of a connected data set from the fields a connector describes, and tells how they changed.
/// </summary>
public static class ConnectedDataSetFields
{
    /// <summary>
    /// Returns a field name consumers can use: letters, digits and underscores, starting with a letter.
    /// </summary>
    /// <param name="sourceName">The source field's name, such as <c>address.city</c>.</param>
    /// <returns>The name, such as <c>address_city</c>.</returns>
    public static string ToFieldName(string sourceName)
    {
        if (string.IsNullOrWhiteSpace(sourceName))
        {
            return "Field";
        }

        var builder = new StringBuilder(sourceName.Length);

        foreach (var character in sourceName.Trim())
        {
            builder.Append(char.IsAsciiLetterOrDigit(character) ? character : '_');
        }

        var name = builder.ToString().Trim('_');

        if (name.Length == 0)
        {
            return "Field";
        }

        return char.IsAsciiLetter(name[0]) ? name : "F_" + name;
    }

    /// <summary>
    /// Creates the fields of a new data set from the source fields.
    /// </summary>
    /// <param name="sourceFields">The fields the connector describes.</param>
    /// <returns>The data set fields, with unique names.</returns>
    public static List<ConnectedDataField> Create(IEnumerable<DataField> sourceFields)
    {
        ArgumentNullException.ThrowIfNull(sourceFields);

        var fields = new List<ConnectedDataField>();

        foreach (var source in sourceFields)
        {
            fields.Add(CreateField(source, fields));
        }

        return fields;
    }

    /// <summary>
    /// Compares a data set's fields with the fields the connector describes now.
    /// </summary>
    /// <param name="fields">The data set's fields.</param>
    /// <param name="sourceFields">The fields the connector describes.</param>
    /// <returns>How the source's fields changed.</returns>
    public static DataSetSchemaChanges Compare(IReadOnlyList<ConnectedDataField> fields, IReadOnlyList<DataField> sourceFields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(sourceFields);

        var changes = new DataSetSchemaChanges();
        var known = fields.Where(field => !string.IsNullOrEmpty(field.SourceName))
            .GroupBy(field => field.SourceName, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var source in sourceFields)
        {
            seen.Add(source.Name);

            if (!known.TryGetValue(source.Name, out var field))
            {
                changes.Added.Add(source.Name);
            }
            else if (field.SourceType != source.Type)
            {
                changes.Changed.Add($"{source.Name} ({field.SourceType} → {source.Type})");
            }
        }

        foreach (var field in fields)
        {
            if (!string.IsNullOrEmpty(field.SourceName) && !seen.Contains(field.SourceName))
            {
                changes.Removed.Add(field.SourceName);
            }
        }

        return changes;
    }

    /// <summary>
    /// Brings a data set's fields in line with the source: added fields join it, a changed source type updates the
    /// field's type unless an administrator chose another one, and a removed field is kept so the consumers that use
    /// it keep working, with empty values.
    /// </summary>
    /// <param name="fields">The data set's fields, changed in place.</param>
    /// <param name="sourceFields">The fields the connector describes.</param>
    public static void Merge(List<ConnectedDataField> fields, IReadOnlyList<DataField> sourceFields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(sourceFields);

        foreach (var source in sourceFields)
        {
            var field = fields.FirstOrDefault(entry => string.Equals(entry.SourceName, source.Name, StringComparison.Ordinal));

            if (field is null)
            {
                fields.Add(CreateField(source, fields));

                continue;
            }

            if (field.SourceType != source.Type)
            {
                // A type the administrator chose stays; a type that only followed the source follows it again.
                if (field.Type == field.SourceType)
                {
                    field.Type = source.Type;
                }

                field.SourceType = source.Type;
            }
        }
    }

    /// <summary>
    /// Returns the data set's fields as consumers see them.
    /// </summary>
    /// <param name="dataSet">The data set.</param>
    /// <param name="identifiersAreKeyFilterable">Whether a single identifier field can be filtered by key at the source.</param>
    /// <returns>The included fields.</returns>
    public static IReadOnlyList<DataField> ToDataFields(ConnectedDataSet dataSet, bool identifiersAreKeyFilterable = false)
    {
        ArgumentNullException.ThrowIfNull(dataSet);

        var included = (dataSet.Fields ?? []).Where(field => field.IsIncluded && !string.IsNullOrEmpty(field.Name)).ToArray();
        var singleIdentifier = included.Count(field => field.IsIdentifier) == 1;

        return included
            .Select(field => new DataField(field.Name, string.IsNullOrWhiteSpace(field.DisplayName) ? field.Name : field.DisplayName, field.Type)
            {
                Description = field.Description,
                IsIdentifier = field.IsIdentifier,
                IsKeyFilterable = identifiersAreKeyFilterable && singleIdentifier && field.IsIdentifier,
            })
            .ToArray();
    }

    /// <summary>
    /// Returns the descriptor of a data set as consumers see it.
    /// </summary>
    /// <param name="dataSet">The data set.</param>
    /// <param name="group">The group it is listed under, such as its connection's name.</param>
    /// <returns>The descriptor.</returns>
    public static DataSetDescriptor ToDescriptor(ConnectedDataSet dataSet, string group)
    {
        ArgumentNullException.ThrowIfNull(dataSet);

        var defaultDateField = dataSet.Fields?.FirstOrDefault(field =>
            field.IsIncluded &&
            string.Equals(field.Name, dataSet.DefaultDateField, StringComparison.Ordinal) &&
            DataValues.IsTemporal(field.Type));

        return new DataSetDescriptor(dataSet.Name, string.IsNullOrWhiteSpace(dataSet.DisplayText) ? dataSet.Name : dataSet.DisplayText, dataSet.Description)
        {
            Group = group,
            DefaultDateField = defaultDateField?.Name,
        };
    }

    private static ConnectedDataField CreateField(DataField source, IReadOnlyCollection<ConnectedDataField> existing)
    {
        var name = ToFieldName(source.Name);
        var unique = name;
        var suffix = 2;

        while (existing.Any(field => string.Equals(field.Name, unique, StringComparison.OrdinalIgnoreCase)))
        {
            unique = $"{name}_{suffix++}";
        }

        return new ConnectedDataField
        {
            Name = unique,
            SourceName = source.Name,
            DisplayName = string.IsNullOrWhiteSpace(source.DisplayName) ? source.Name : source.DisplayName,
            Description = source.Description,
            Type = source.Type,
            SourceType = source.Type,
            IsIdentifier = source.IsIdentifier,
            IsIncluded = true,
        };
    }
}

/// <summary>
/// How the fields of a data set's source changed.
/// </summary>
public sealed class DataSetSchemaChanges
{
    /// <summary>
    /// Gets the source fields that are new.
    /// </summary>
    public IList<string> Added { get; } = [];

    /// <summary>
    /// Gets the source fields that are gone.
    /// </summary>
    public IList<string> Removed { get; } = [];

    /// <summary>
    /// Gets the source fields whose type changed, with the old and new types.
    /// </summary>
    public IList<string> Changed { get; } = [];

    /// <summary>
    /// Gets a value indicating whether anything changed.
    /// </summary>
    public bool HasChanges => Added.Count > 0 || Removed.Count > 0 || Changed.Count > 0;

    /// <inheritdoc/>
    public override string ToString()
    {
        var parts = new List<string>();

        if (Added.Count > 0)
        {
            parts.Add("Added: " + string.Join(", ", Added));
        }

        if (Removed.Count > 0)
        {
            parts.Add("Removed: " + string.Join(", ", Removed));
        }

        if (Changed.Count > 0)
        {
            parts.Add("Type changed: " + string.Join(", ", Changed));
        }

        return string.Join(". ", parts);
    }
}
