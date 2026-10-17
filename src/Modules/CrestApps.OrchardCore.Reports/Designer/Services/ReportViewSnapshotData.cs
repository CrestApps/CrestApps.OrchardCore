using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Reports.DataSources;
using CrestApps.OrchardCore.Reports.Designer.Models;

namespace CrestApps.OrchardCore.Reports.Designer.Services;

/// <summary>
/// Converts a view's result to the rows a <see cref="ReportViewSnapshot"/> stores and back. Values are stored as JSON
/// and converted back to the CLR type of their field when read, so a report reads the same values from a snapshot as
/// from the live view.
/// </summary>
public static class ReportViewSnapshotData
{
    /// <summary>
    /// Stores a view's result in a snapshot, keeping at most <paramref name="maxRows"/> rows.
    /// </summary>
    /// <param name="snapshot">The snapshot to fill.</param>
    /// <param name="table">The view's result.</param>
    /// <param name="maxRows">The most rows to keep.</param>
    public static void Fill(ReportViewSnapshot snapshot, ReportDataTable table, int maxRows)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(table);

        var limit = Math.Max(1, maxRows);
        var rows = new JsonArray();

        foreach (var row in table.Rows.Take(limit))
        {
            var values = new JsonArray();

            foreach (var value in row)
            {
                values.Add(ToNode(value));
            }

            rows.Add(values);
        }

        snapshot.Fields = table.Fields
            .Select(field => new ReportViewSnapshotField
            {
                Name = field.Name,
                Label = field.DisplayName,
                DataType = field.DataType,
            })
            .ToList();
        snapshot.Rows = rows;
        snapshot.RowCount = rows.Count;
        snapshot.Truncated = table.Truncated || table.Rows.Count > limit;
    }

    /// <summary>
    /// Determines whether a snapshot was stored with the fields a view now has: the same names, in the same order,
    /// with the same data types. A snapshot of a view that was changed since must not be read.
    /// </summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="fields">The fields the view has now.</param>
    /// <returns><see langword="true"/> when the snapshot matches.</returns>
    public static bool Matches(ReportViewSnapshot snapshot, IList<ReportFieldDescriptor> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        if (snapshot?.Fields is null || snapshot.Rows is null || snapshot.Fields.Count != fields.Count)
        {
            return false;
        }

        for (var index = 0; index < fields.Count; index++)
        {
            if (!string.Equals(snapshot.Fields[index].Name, fields[index].Name, StringComparison.Ordinal) ||
                snapshot.Fields[index].DataType != fields[index].DataType)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Reads a snapshot's rows as a data table.
    /// </summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="requestedFields">The fields to return, or an empty set for all of them.</param>
    /// <param name="maxRows">The most rows to return.</param>
    /// <returns>The table, whose values have the CLR types of their fields and whose date-times are in UTC.</returns>
    public static ReportDataTable ToTable(ReportViewSnapshot snapshot, ISet<string> requestedFields, int maxRows)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var limit = Math.Max(1, maxRows);
        var selected = new List<(int Index, ReportViewSnapshotField Field)>();

        for (var index = 0; index < snapshot.Fields.Count; index++)
        {
            var field = snapshot.Fields[index];

            if (requestedFields is null || requestedFields.Count == 0 || requestedFields.Contains(field.Name))
            {
                selected.Add((index, field));
            }
        }

        var source = snapshot.Rows ?? [];
        var rows = new List<object[]>(Math.Min(source.Count, limit));

        foreach (var node in source.Take(limit))
        {
            var values = node as JsonArray;
            var row = new object[selected.Count];

            for (var column = 0; column < selected.Count; column++)
            {
                var (index, field) = selected[column];

                row[column] = values is not null && index < values.Count ? FromNode(values[index], field.DataType) : null;
            }

            rows.Add(row);
        }

        return new ReportDataTable
        {
            Fields = selected
                .Select(entry => new ReportFieldDescriptor(entry.Field.Name, entry.Field.Label, entry.Field.DataType))
                .ToList(),
            Rows = rows,
            Truncated = snapshot.Truncated || source.Count > limit,
        };
    }

    /// <summary>
    /// Converts a value to the JSON a snapshot stores.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The JSON node, or <see langword="null"/> for a missing value.</returns>
    public static JsonNode ToNode(object value)
    {
        return value switch
        {
            null or DBNull => null,
            string text => JsonValue.Create(text),
            bool flag => JsonValue.Create(flag),
            long number => JsonValue.Create(number),
            int number => JsonValue.Create(number),
            short number => JsonValue.Create(number),
            byte number => JsonValue.Create(number),
            decimal number => JsonValue.Create(number),
            double number => double.IsFinite(number) ? JsonValue.Create(number) : null,
            float number => float.IsFinite(number) ? JsonValue.Create(number) : null,

            // Round-trip text keeps the kind: a UTC date-time ends in Z and reads back as UTC.
            DateTime date => JsonValue.Create(date.ToString("O", CultureInfo.InvariantCulture)),
            DateTimeOffset offset => JsonValue.Create(offset.UtcDateTime.ToString("O", CultureInfo.InvariantCulture)),
            DateOnly day => JsonValue.Create(day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            JsonNode node => node.DeepClone(),
            IFormattable formattable => JsonValue.Create(formattable.ToString(null, CultureInfo.InvariantCulture)),
            _ => JsonValue.Create(value.ToString()),
        };
    }

    /// <summary>
    /// Converts stored JSON back to the CLR type of a field.
    /// </summary>
    /// <param name="node">The stored JSON.</param>
    /// <param name="dataType">The field's data type.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    public static object FromNode(JsonNode node, ReportDataType dataType)
    {
        var value = ReportDataValues.Coerce(node, dataType);

        // A view hands its date-times back in UTC.
        return dataType == ReportDataType.DateTime && value is DateTime date && date.Kind != DateTimeKind.Utc
            ? (date.Kind == DateTimeKind.Local ? date.ToUniversalTime() : DateTime.SpecifyKind(date, DateTimeKind.Utc))
            : value;
    }
}
