using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CrestApps.Core.Entities;

/// <summary>
/// Reads and writes typed aspects inside a <see cref="JsonObject"/> property bag.
/// </summary>
/// <remarks>
/// <para>
/// The key is the aspect type's name, which is what the host's entity helpers used, so a document
/// written before a model stopped deriving from that base still reads back.
/// </para>
/// <para>
/// Values are read and written with this type's own settings rather than the serializer's defaults,
/// because the defaults disagree with the host on three things a stored aspect can carry. An enum is
/// written as a number rather than as its name, a null property is written rather than omitted, and a
/// <see cref="DateTime"/> is written in the round-trip shape rather than the shape the host's converter
/// produced. The enum case loses data outright: a bag holds whatever any version wrote into it, so an
/// aspect that no longer deserializes is read as absent rather than as an error, and every value in it
/// is lost. A test measures each of these against the host helper rather than describing them.
/// </para>
/// <para>
/// The settings are this type's own, and read-only, rather than
/// <see cref="ExtensibleEntityExtensions.JsonSerializerOptions"/>. That static is settable, and a host
/// or an unrelated feature configuring it would silently change the shape of durable tenant data on the
/// next save, with no migration and no failing test.
/// </para>
/// <para>
/// One difference remains and is deliberate: a member typed as <see cref="object"/> reads back as a
/// <see cref="JsonElement"/> here, where the host materialized it as a CLR primitive. The written text
/// is the same either way, and no aspect stored through this bag declares such a member. Reproducing it
/// would mean carrying a copy of the host's dynamic converter for a case that does not arise.
/// </para>
/// </remarks>
public static class JsonPropertyBag
{
    /// <summary>
    /// The settings a stored aspect is written with and read back with.
    /// </summary>
    private static readonly JsonSerializerOptions _serializerOptions = CreateSerializerOptions();

    /// <summary>
    /// Builds the settings that reproduce the host's stored text.
    /// </summary>
    /// <returns>A read-only options instance.</returns>
    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = ExtensibleEntityJsonOptions.CreateDefaultSerializerOptions();

        options.Converters.Add(new StoredDateTimeConverter());

        // The reflection resolver is populated here rather than left to the first serialization, because
        // marking the instance read-only without one throws, and read-only is the point: these settings
        // describe an on-disk format and nothing may add a converter to them later.
        options.MakeReadOnly(populateMissingResolver: true);

        return options;
    }

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
            aspect = node.Deserialize<T>(_serializerOptions);
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

        properties[typeof(T).Name] = JsonSerializer.SerializeToNode(aspect, _serializerOptions);
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

    /// <summary>
    /// Writes a <see cref="DateTime"/> the way the host's entity helper wrote it.
    /// </summary>
    /// <remarks>
    /// The host stamped the clock components it was given with a <c>Z</c> suffix and second precision,
    /// whatever <see cref="DateTimeKind"/> the value carried and whatever sub-second ticks it had. It did
    /// not convert to UTC first, so a value is written with the same wall-clock reading it arrived with.
    /// That is reproduced rather than corrected: a document written here and one written before the move
    /// have to be the same text, and changing the format would silently rewrite the shape of every stored
    /// aspect that carries a timestamp.
    /// </remarks>
    private sealed class StoredDateTimeConverter : JsonConverter<DateTime>
    {
        private const string StoredFormat = "yyyy-MM-ddTHH:mm:ss";

        /// <inheritdoc />
        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.GetDateTime();

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer);

            writer.WriteStringValue(value.ToString(StoredFormat, CultureInfo.InvariantCulture) + "Z");
        }
    }
}
