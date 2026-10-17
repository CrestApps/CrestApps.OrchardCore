using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Reports.DataSources;

/// <summary>
/// Describes the fields a data set declares with <c>AddField</c>, which returns the field.
/// </summary>
public static class DataFieldExtensions
{
    /// <summary>
    /// Marks a field as identifying a record, of its own data set or of the data sets it references, so consumers can
    /// suggest to join them.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <param name="references">The fields of other data sets the field holds the identifiers of.</param>
    /// <returns>The field.</returns>
    public static DataField Identifier(this DataField field, params DataFieldReference[] references)
    {
        ArgumentNullException.ThrowIfNull(field);

        field.IsIdentifier = true;

        foreach (var reference in references ?? [])
        {
            field.References.Add(reference);
        }

        return field;
    }

    /// <summary>
    /// Adds the data sets the fields of a data set reference to the data sets it links to, so consumers can tell which
    /// data sets are related before reading their fields.
    /// </summary>
    /// <param name="descriptor">The description of the data set.</param>
    /// <param name="fields">The fields of the data set.</param>
    public static void AddReferencesOf(this DataSetDescriptor descriptor, IEnumerable<DataField> fields)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(fields);

        foreach (var reference in fields.SelectMany(field => field.References))
        {
            if (!descriptor.References.Any(existing => existing.Source == reference.Source && existing.DataSet == reference.DataSet))
            {
                descriptor.References.Add(reference);
            }
        }
    }

    /// <summary>
    /// Marks the fields a data set loads only the records of a list of keys for, such as the keys of the rows a join
    /// needs. See <see cref="DataConditions.Keys"/>.
    /// </summary>
    /// <param name="fields">The fields of the data set.</param>
    /// <param name="names">The names of the key filterable fields.</param>
    public static void MarkKeyFilterable(this IReadOnlyList<DataField> fields, IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(names);

        var keys = names.ToHashSet(StringComparer.Ordinal);

        foreach (var field in fields)
        {
            if (keys.Contains(field.Name))
            {
                field.IsKeyFilterable = true;
            }
        }
    }
}
