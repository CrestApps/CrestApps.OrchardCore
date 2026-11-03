using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.DataConnectors.Models;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.DataConnectors.Storage;

/// <summary>
/// Converts rows between the typed values of a data set and the JSON the row store keeps.
/// </summary>
internal static class DataSetRows
{
    /// <summary>
    /// Returns the identity key of a row: a hash of its identifier values, or <see langword="null"/> when the data set
    /// has no identifier.
    /// </summary>
    /// <param name="values">The identifier values, in the order of the data set's identifier fields.</param>
    /// <returns>The key.</returns>
    public static string ToKey(IReadOnlyList<object> values)
    {
        if (values is null || values.Count == 0)
        {
            return null;
        }

        var text = string.Join('\u001f', values.Select(DataValues.ToKey));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    /// <summary>
    /// Returns a row as the JSON object the store keeps, keyed by data set field name.
    /// </summary>
    /// <param name="fields">The fields of the row, in order.</param>
    /// <param name="row">The typed values.</param>
    /// <returns>The JSON object.</returns>
    public static JsonObject ToJson(IReadOnlyList<ConnectedDataField> fields, object[] row)
    {
        var json = new JsonObject();

        for (var index = 0; index < fields.Count && index < row.Length; index++)
        {
            json[fields[index].Name] = ToNode(row[index]);
        }

        return json;
    }

    /// <summary>
    /// Returns a stored value as the typed value of its field.
    /// </summary>
    /// <param name="data">The stored row.</param>
    /// <param name="field">The field.</param>
    /// <returns>The typed value, or <see langword="null"/>.</returns>
    public static object Read(JsonObject data, DataField field)
    {
        if (data is null || !data.TryGetPropertyValue(field.Name, out var node) || node is null)
        {
            return null;
        }

        var value = DataValues.Coerce(node, field.Type);

        return value is DateTime { Kind: not DateTimeKind.Utc } date
            ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
            : value;
    }

    private static JsonValue ToNode(object value)
    {
        return value switch
        {
            null => null,
            string text => JsonValue.Create(text),
            bool flag => JsonValue.Create(flag),
            long number => JsonValue.Create(number),
            int number => JsonValue.Create(number),
            decimal number => JsonValue.Create(number),
            double number => double.IsFinite(number) ? JsonValue.Create(number) : null,
            DateTime date => JsonValue.Create(date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
            _ => JsonValue.Create(DataValues.ToText(value)),
        };
    }
}
