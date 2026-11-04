using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Core.Deployments;

/// <summary>
/// Serializes records kept in a plain catalog store to and from the JSON shape a deployment plan carries.
/// </summary>
/// <remarks>
/// Unlike <see cref="CatalogDeploymentSerializer"/>, which serves configuration entries whose manager stamps them, a
/// record is carried with its dates and history, because a plain store has no handler to stamp it again. Only the
/// numeric <c>Id</c>, the database document id of the storing site, is left out: it would point at another document,
/// or at none, on the importing site. Records are matched by <see cref="CatalogItem.ItemId"/> instead.
/// </remarks>
public static class CatalogRecordSerializer
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _properties = new();

    /// <summary>
    /// The name of the member that holds the database document id on records that keep it.
    /// </summary>
    public const string DocumentIdMember = "Id";

    /// <summary>
    /// Serializes a record into the JSON shape stored in a deployment plan.
    /// </summary>
    /// <param name="record">The record to serialize.</param>
    /// <returns>A JSON object with every portable member of the record.</returns>
    public static JsonObject Export(object record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var node = JsonSerializer.SerializeToNode(record, record.GetType(), CatalogDeploymentSerializer.Options)?.AsObject()
            ?? [];

        if (HasDocumentId(record.GetType()))
        {
            node.Remove(DocumentIdMember);
        }

        return node;
    }

    /// <summary>
    /// Reads a new record from its JSON, without its document id.
    /// </summary>
    /// <typeparam name="T">The record type.</typeparam>
    /// <param name="data">The JSON of the record.</param>
    /// <returns>The record, or <see langword="null"/> when the JSON holds none.</returns>
    public static T Read<T>(JsonObject data)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(data);

        var record = data.Deserialize<T>(CatalogDeploymentSerializer.Options);

        if (record is not null && HasDocumentId(typeof(T)))
        {
            var documentId = typeof(T).GetProperty(DocumentIdMember);

            documentId.SetValue(record, documentId.PropertyType == typeof(long) ? 0L : 0);
        }

        return record;
    }

    /// <summary>
    /// Applies every portable member present in the JSON onto a stored record, leaving its identifier and its document
    /// id as they are.
    /// </summary>
    /// <param name="record">The stored record.</param>
    /// <param name="data">The JSON to read the values from.</param>
    public static void Populate(object record, JsonObject data)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(data);

        var type = record.GetType();
        var source = data.Deserialize(type, CatalogDeploymentSerializer.Options);

        if (source is null)
        {
            return;
        }

        foreach (var property in GetProperties(type))
        {
            var name = property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name;

            if (data.ContainsKey(name))
            {
                property.SetValue(record, property.GetValue(source));
            }
        }
    }

    private static bool HasDocumentId(Type type)
        => type.GetProperty(DocumentIdMember, BindingFlags.Public | BindingFlags.Instance) is { } property
            && (property.PropertyType == typeof(long) || property.PropertyType == typeof(int));

    private static PropertyInfo[] GetProperties(Type type)
        => _properties.GetOrAdd(type, static key => key
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead
                && property.CanWrite
                && property.GetIndexParameters().Length == 0
                && property.GetCustomAttribute<JsonIgnoreAttribute>() is null
                && property.Name != nameof(CatalogItem.ItemId)
                && !(property.Name == DocumentIdMember && HasDocumentId(key)))
            .ToArray());
}
