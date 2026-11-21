using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Core.Deployments;

namespace CrestApps.OrchardCore.Surveys.Recipes;

/// <summary>
/// Reads and writes the JSON of Surveys recipe records.
/// </summary>
internal static class SurveysRecipeJson
{
    /// <summary>
    /// Reads a text property.
    /// </summary>
    /// <param name="token">The record.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The trimmed text, or <see langword="null"/>.</returns>
    public static string String(JsonObject token, string name)
    {
        if (token is null || !token.TryGetPropertyValue(name, out var node) || node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<string>(out var text))
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }

        return value.ToJsonString().Trim('"');
    }

    /// <summary>
    /// Reads a whole number.
    /// </summary>
    /// <param name="token">The record.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The number, or <see langword="null"/>.</returns>
    public static int? Int(JsonObject token, string name)
    {
        if (token is null || !token.TryGetPropertyValue(name, out var node) || node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<int>(out var number))
        {
            return number;
        }

        return value.TryGetValue<string>(out var text) && int.TryParse(text, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out number)
            ? number
            : null;
    }

    /// <summary>
    /// Reads an array of records.
    /// </summary>
    /// <param name="token">The record.</param>
    /// <param name="name">The property name.</param>
    /// <returns>The records.</returns>
    public static IEnumerable<JsonObject> Array(JsonObject token, string name)
        => token is not null && token.TryGetPropertyValue(name, out var node) && node is JsonArray array
            ? array.OfType<JsonObject>()
            : [];

    /// <summary>
    /// Reads a record as a model, leaving out the properties a step handles itself.
    /// </summary>
    /// <typeparam name="T">The model type.</typeparam>
    /// <param name="token">The record.</param>
    /// <param name="exclude">The properties to leave out.</param>
    /// <returns>The model.</returns>
    public static T Read<T>(JsonObject token, params string[] exclude)
        where T : class
    {
        var copy = token.DeepClone().AsObject();

        foreach (var name in exclude)
        {
            copy.Remove(name);
        }

        return copy.Deserialize<T>(CatalogDeploymentSerializer.Options);
    }

    /// <summary>
    /// Copies the properties a record holds onto a model already on the site, leaving out the properties a step handles
    /// itself. The model stays the instance the session tracks, so saving it updates the document.
    /// </summary>
    /// <param name="entry">The model.</param>
    /// <param name="token">The record.</param>
    /// <param name="exclude">The properties to leave out.</param>
    public static void Populate(object entry, JsonObject token, params string[] exclude)
    {
        var copy = token.DeepClone().AsObject();

        foreach (var name in exclude)
        {
            copy.Remove(name);
        }

        CatalogDeploymentSerializer.Populate(entry, copy);
    }

    /// <summary>
    /// Writes a model as a record, leaving out the properties that belong to one site.
    /// </summary>
    /// <param name="entry">The model.</param>
    /// <param name="exclude">More properties to leave out.</param>
    /// <returns>The record.</returns>
    public static JsonObject Write(object entry, params string[] exclude)
    {
        var node = CatalogDeploymentSerializer.Export(entry);

        foreach (var name in exclude)
        {
            node.Remove(name);
        }

        return node;
    }
}
