using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.DataConnectors.Models;
using OrchardCore;

namespace CrestApps.OrchardCore.DataConnectors.Recipes;

/// <summary>
/// Reads and writes the entries of the data connector recipe steps, which a deployment plan exports in the same shape.
/// </summary>
internal static class DataConnectorRecipeJson
{
    /// <summary>
    /// Names are written as declared and read in any case, enums are written by name and read by name or number, and
    /// empty members are left out.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Reads the entries of a step.
    /// </summary>
    /// <typeparam name="T">The entry's type.</typeparam>
    /// <param name="step">The step.</param>
    /// <param name="memberName">The member that holds the entries.</param>
    /// <returns>Each entry, or <see langword="null"/> where one isn't an object.</returns>
    public static IEnumerable<(JsonNode Node, T Entry)> ReadEntries<T>(JsonObject step, string memberName)
        where T : class
    {
        if (step?[memberName] is not JsonArray entries)
        {
            yield break;
        }

        foreach (var node in entries)
        {
            yield return (node, node is JsonObject entry ? entry.Deserialize<T>(Options) : null);
        }
    }

    /// <summary>
    /// Copies the members of an entry's properties onto a record, so a connector's settings travel with it. Members the
    /// entry doesn't carry are kept.
    /// </summary>
    /// <param name="target">The record's properties.</param>
    /// <param name="source">The entry's properties.</param>
    public static void MergeProperties(IDictionary<string, object> target, JsonObject source)
    {
        if (target is null || source is null)
        {
            return;
        }

        foreach (var property in source)
        {
            target[property.Key] = property.Value?.DeepClone();
        }
    }

    /// <summary>
    /// Returns the identifier a new record is stored under: the entry's, so other entries and steps that refer to it keep
    /// resolving, unless it doesn't fit the index, in which case a new one is made.
    /// </summary>
    /// <param name="itemId">The entry's identifier.</param>
    /// <returns>The identifier.</returns>
    public static string NewItemId(string itemId)
        => !string.IsNullOrWhiteSpace(itemId) && itemId.Length <= 26 && itemId.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
            ? itemId
            : IdGenerator.GenerateId();

    /// <summary>
    /// Copies a record's properties into an entry, as the record stores them, so the connector reads them back alike.
    /// </summary>
    /// <param name="properties">The record's properties.</param>
    /// <returns>The properties, or <see langword="null"/> when there are none.</returns>
    public static JsonObject ExportProperties(IDictionary<string, object> properties)
        => properties is { Count: > 0 } ? JsonSerializer.SerializeToNode(properties)?.AsObject() : null;
}

/// <summary>
/// A connection in the <c>DataConnection</c> recipe step.
/// </summary>
internal sealed class DataConnectionRecipeEntry
{
    public string ItemId { get; set; }

    public string Name { get; set; }

    public string DisplayText { get; set; }

    public string Description { get; set; }

    public string Connector { get; set; }

    /// <summary>
    /// Gets or sets the connector's settings, keyed by the name of their type, as the connection stores them.
    /// </summary>
    public JsonObject Properties { get; set; }

    /// <summary>
    /// Gets or sets the secrets in clear text, keyed by name. They are protected when stored, and never exported: an
    /// empty or missing secret keeps the one the connection already stores.
    /// </summary>
    public Dictionary<string, string> Secrets { get; set; }

    public DateTime? CreatedUtc { get; set; }

    public DateTime? ModifiedUtc { get; set; }

    public string Author { get; set; }
}

/// <summary>
/// A data set in the <c>ConnectedDataSet</c> recipe step.
/// </summary>
internal sealed class ConnectedDataSetRecipeEntry
{
    public string ItemId { get; set; }

    public string Name { get; set; }

    public string DisplayText { get; set; }

    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the connection the data set reads.
    /// </summary>
    public string ConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the connection, used when no connection has <see cref="ConnectionId"/>, such as
    /// on a site whose connection was created by hand.
    /// </summary>
    public string ConnectionName { get; set; }

    public string Resource { get; set; }

    public DataSetAccessMode? AccessMode { get; set; }

    public bool? IsEnabled { get; set; }

    public List<ConnectedDataField> Fields { get; set; }

    public string DefaultDateField { get; set; }

    public DataSetRefreshSettings Refresh { get; set; }

    /// <summary>
    /// Gets or sets the connector's settings of the data set, keyed by the name of their type.
    /// </summary>
    public JsonObject Properties { get; set; }

    public DateTime? CreatedUtc { get; set; }

    public DateTime? ModifiedUtc { get; set; }

    public string Author { get; set; }
}
