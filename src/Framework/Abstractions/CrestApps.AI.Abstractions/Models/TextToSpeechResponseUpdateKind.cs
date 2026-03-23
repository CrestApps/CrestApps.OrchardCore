using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.AI.Models;

/// <summary>
/// Describes the intended purpose of a specific update during streaming of text to speech updates.
/// </summary>
[JsonConverter(typeof(Converter))]
public readonly struct TextToSpeechResponseUpdateKind : IEquatable<TextToSpeechResponseUpdateKind>
{
    /// <summary>Gets when the generated audio speech session is opened.</summary>
    public static TextToSpeechResponseUpdateKind SessionOpen { get; } = new("sessionopen");

    /// <summary>Gets when a non-blocking error occurs during text to speech updates.</summary>
    public static TextToSpeechResponseUpdateKind Error { get; } = new("error");

    /// <summary>Gets when the audio update is in progress.</summary>
    public static TextToSpeechResponseUpdateKind AudioUpdating { get; } = new("audioupdating");

    /// <summary>Gets when an audio chunk has been fully generated.</summary>
    public static TextToSpeechResponseUpdateKind AudioUpdated { get; } = new("audioupdated");

    /// <summary>Gets when the generated audio speech session is closed.</summary>
    public static TextToSpeechResponseUpdateKind SessionClose { get; } = new("sessionclose");

    /// <summary>
    /// Gets the value associated with this <see cref="TextToSpeechResponseUpdateKind"/>.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TextToSpeechResponseUpdateKind"/> struct with the provided value.
    /// </summary>
    /// <param name="value">The value to associate with this <see cref="TextToSpeechResponseUpdateKind"/>.</param>
    [JsonConstructor]
    public TextToSpeechResponseUpdateKind(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    /// <summary>Returns whether two instances are equivalent.</summary>
    public static bool operator ==(TextToSpeechResponseUpdateKind left, TextToSpeechResponseUpdateKind right)
        => left.Equals(right);

    /// <summary>Returns whether two instances are not equivalent.</summary>
    public static bool operator !=(TextToSpeechResponseUpdateKind left, TextToSpeechResponseUpdateKind right)
        => !(left == right);

    /// <inheritdoc/>
    public override bool Equals(object obj)
        => obj is TextToSpeechResponseUpdateKind otherKind && Equals(otherKind);

    /// <inheritdoc/>
    public bool Equals(TextToSpeechResponseUpdateKind other)
        => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    /// <inheritdoc/>
    public override int GetHashCode()
        => Value is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>Provides a <see cref="JsonConverter{T}"/> for serializing <see cref="TextToSpeechResponseUpdateKind"/> instances.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class Converter : JsonConverter<TextToSpeechResponseUpdateKind>
    {
        /// <inheritdoc />
        public override TextToSpeechResponseUpdateKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            new(reader.GetString());

        /// <inheritdoc />
        public override void Write(Utf8JsonWriter writer, TextToSpeechResponseUpdateKind value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value.Value);
    }
}
