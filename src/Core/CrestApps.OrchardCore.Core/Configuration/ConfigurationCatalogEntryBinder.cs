using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Core.Configuration;

/// <summary>
/// Serializes catalog entries to and from the JSON shape used by recipes and deployment plans.
/// </summary>
/// <remarks>
/// Binding is driven by reflection over the entry type rather than by a hand-written property list so that a
/// property added to an entry is exported and imported without any further change. Members owned by the hosting
/// environment are never carried across environments; the target environment stamps them.
/// </remarks>
public static class ConfigurationCatalogEntryBinder
{
    private static readonly ConcurrentDictionary<Type, BindableProperty[]> _properties = new();

    /// <summary>
    /// A copy of the house serializer that writes null members instead of omitting them. Omitting them would make an
    /// import strictly additive: a setting cleared in the source environment would keep its stale value everywhere the
    /// plan was replayed, because the member would simply be absent from the plan.
    /// </summary>
    private static readonly JsonSerializerOptions _options = CreateOptions();

    /// <summary>
    /// Gets the names of the members that belong to the environment that stores the entry rather than to its configuration.
    /// </summary>
    public static readonly IReadOnlySet<string> EnvironmentOwnedMembers = new HashSet<string>(StringComparer.Ordinal)
    {
        "CreatedUtc",
        "ModifiedUtc",
        "OwnerId",
        "Author",
    };

    /// <summary>
    /// Serializes an entry into the JSON shape stored in a recipe step.
    /// </summary>
    /// <param name="entry">The entry to serialize.</param>
    /// <returns>A JSON object containing every portable member of the entry.</returns>
    public static JsonObject Export(object entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var node = JsonSerializer.SerializeToNode(entry, entry.GetType(), _options)?.AsObject()
            ?? new JsonObject();

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
    /// <param name="pristine">
    /// An entry the owning manager produced from the same data with nothing else supplied, used to tell a value the
    /// manager derived from the data apart from one it merely defaulted. A member the manager derived is left alone,
    /// because the manager owns the canonical form of the values it populates - a trimmed name, a phone number in
    /// E.164 - and overwriting it with the raw text in the plan would undo that normalization. A member that still
    /// matches the manager's default is taken from the data, so a plan can still turn a defaulted flag off. When no
    /// pristine entry is supplied every member present in the data is applied.
    /// </param>
    public static void Apply(object entry, JsonObject data, object pristine = null)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (data is null)
        {
            return;
        }

        var type = entry.GetType();
        object source = null;

        foreach (var property in GetProperties(type))
        {
            if (!data.ContainsKey(property.JsonName))
            {
                continue;
            }

            if (pristine is not null && IsOwnedByManager(property, entry, pristine))
            {
                continue;
            }

            source ??= JsonSerializer.Deserialize(data, type, _options);

            if (source is null)
            {
                return;
            }

            property.Member.SetValue(entry, property.Member.GetValue(source));
        }
    }

    private static bool IsOwnedByManager(BindableProperty property, object entry, object pristine)
    {
        if (pristine.GetType() != entry.GetType())
        {
            return false;
        }

        var current = property.Member.GetValue(entry);
        var defaulted = property.Member.GetValue(pristine);

        return !JsonNode.DeepEquals(
            JsonSerializer.SerializeToNode(current, property.Member.PropertyType, _options),
            JsonSerializer.SerializeToNode(defaulted, property.Member.PropertyType, _options));
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JOptions.Default)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        };

        return options;
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
