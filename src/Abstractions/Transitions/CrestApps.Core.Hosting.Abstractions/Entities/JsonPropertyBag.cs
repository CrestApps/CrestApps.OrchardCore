using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrestApps.Core.Entities;

/// <summary>
/// Reads and writes typed aspects inside a <see cref="JsonObject"/> property bag.
/// </summary>
/// <remarks>
/// The key is the aspect type's name and values are written with the default serializer settings,
/// which is the shape Orchard Core's entity helpers produced. Documents written before a model
/// stopped deriving from that base therefore still read back unchanged.
/// </remarks>
public static class JsonPropertyBag
{
    /// <summary>
    /// Tries to read the stored aspect.
    /// </summary>
    /// <typeparam name="T">The aspect type.</typeparam>
    /// <param name="properties">The property bag.</param>
    /// <param name="aspect">The stored aspect when one is present; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the aspect was read.</returns>
    public static bool TryGet<T>(JsonObject properties, out T aspect)
    {
        aspect = default;

        if (properties is null || !properties.TryGetPropertyValue(typeof(T).Name, out var node) || node is null)
        {
            return false;
        }

        try
        {
            aspect = node.Deserialize<T>();
        }
        catch (JsonException)
        {
            // A property bag holds whatever any version wrote into it. An aspect that no longer
            // deserializes is treated as absent rather than fatal, which is how the Orchard helper
            // behaved and what callers already handle.
            return false;
        }

        return aspect is not null;
    }

    /// <summary>
    /// Gets the stored aspect, or a new instance when the bag does not carry one.
    /// </summary>
    /// <typeparam name="T">The aspect type.</typeparam>
    /// <param name="properties">The property bag.</param>
    /// <returns>The stored aspect, or a new instance.</returns>
    public static T GetOrCreate<T>(JsonObject properties)
        where T : new()
        => TryGet<T>(properties, out var aspect) ? aspect : new T();

    /// <summary>
    /// Stores the aspect, replacing any previous value.
    /// </summary>
    /// <typeparam name="T">The aspect type.</typeparam>
    /// <param name="properties">The property bag.</param>
    /// <param name="aspect">The aspect to store.</param>
    public static void Put<T>(JsonObject properties, T aspect)
    {
        ArgumentNullException.ThrowIfNull(properties);

        properties[typeof(T).Name] = JsonSerializer.SerializeToNode(aspect);
    }

    /// <summary>
    /// Reads the stored aspect, applies a change to it, and writes it back.
    /// </summary>
    /// <typeparam name="T">The aspect type.</typeparam>
    /// <param name="properties">The property bag.</param>
    /// <param name="action">The change to apply.</param>
    public static void Alter<T>(JsonObject properties, Action<T> action)
        where T : new()
    {
        ArgumentNullException.ThrowIfNull(action);

        var aspect = GetOrCreate<T>(properties);
        action(aspect);
        Put(properties, aspect);
    }
}
