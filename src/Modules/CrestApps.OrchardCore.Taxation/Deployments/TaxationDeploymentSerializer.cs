using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using OrchardCore.Json;

namespace CrestApps.OrchardCore.Taxation.Deployments;

/// <summary>
/// Serializes taxation catalog entries to and from the JSON shape a deployment plan carries.
/// </summary>
/// <remarks>
/// Binding is driven by reflection over the entry type rather than by a hand-written property list, so a property
/// added to an entry travels between environments without any further change to the deployment source or the recipe
/// step. Members owned by the environment that stores the entry are never carried; the target environment stamps them.
/// </remarks>
internal static class TaxationDeploymentSerializer
{
    private static readonly ConcurrentDictionary<Type, BindableProperty[]> _properties = new();

    /// <summary>
    /// The options used to export an entry. Null members are written rather than omitted, so a value cleared in the
    /// source environment clears wherever the plan is replayed instead of keeping its stale value.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new(JOptions.Default)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <summary>
    /// Gets the names of the members that belong to the environment that stores the entry rather than to its configuration.
    /// </summary>
    public static readonly IReadOnlySet<string> EnvironmentOwnedMembers = new HashSet<string>(StringComparer.Ordinal)
    {
        "CreatedUtc",
        "ModifiedUtc",
        "OwnerId",
        "Author",
        "Version",
    };

    /// <summary>
    /// Serializes an entry into the JSON shape stored in a deployment plan.
    /// </summary>
    /// <param name="entry">The entry to serialize.</param>
    /// <returns>A JSON object containing every portable member of the entry.</returns>
    public static JsonObject Export(object entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var node = JsonSerializer.SerializeToNode(entry, entry.GetType(), Options)?.AsObject()
            ?? [];

        foreach (var member in EnvironmentOwnedMembers)
        {
            node.Remove(member);
        }

        return node;
    }

    /// <summary>
    /// Applies every portable member present in the given JSON onto an existing entry.
    /// </summary>
    /// <param name="entry">The entry to populate.</param>
    /// <param name="data">The JSON to read the values from.</param>
    public static void Populate(object entry, JsonNode data)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (data is not JsonObject json)
        {
            return;
        }

        var type = entry.GetType();
        object source = null;

        foreach (var property in GetProperties(type))
        {
            if (!json.ContainsKey(property.JsonName))
            {
                continue;
            }

            source ??= JsonSerializer.Deserialize(json, type, Options);

            if (source is null)
            {
                return;
            }

            property.Member.SetValue(entry, property.Member.GetValue(source));
        }
    }

    private static BindableProperty[] GetProperties(Type type)
        => _properties.GetOrAdd(type, static key => key
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead
                && property.CanWrite
                && property.GetIndexParameters().Length == 0
                && property.GetCustomAttribute<JsonIgnoreAttribute>() is null
                && !EnvironmentOwnedMembers.Contains(property.Name)
                && property.Name != "ItemId")
            .Select(property => new BindableProperty(property, property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name))
            .ToArray());

    private sealed record BindableProperty(PropertyInfo Member, string JsonName);
}
