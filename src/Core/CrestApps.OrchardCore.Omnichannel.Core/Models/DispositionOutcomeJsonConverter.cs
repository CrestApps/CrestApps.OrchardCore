using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Reads a stored <see cref="DispositionOutcome"/> without failing on a value the code no longer has.
/// </summary>
/// <remarks>
/// Every disposition lives in one stored document, so one value that cannot be read fails the read of all of them: live,
/// a disposition saved with an outcome that was later removed made the dispositions page, and everything that chooses a
/// disposition, fail. A value that is not an outcome any more reads as <see cref="DispositionOutcome.None"/>, which is what
/// it now means: the platform no longer applies that disposition on its own.
/// </remarks>
public sealed class DispositionOutcomeJsonConverter : JsonConverter<DispositionOutcome>
{
    /// <inheritdoc/>
    public override DispositionOutcome Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                var name = reader.GetString();

                return Enum.TryParse<DispositionOutcome>(name, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                    ? parsed
                    : DispositionOutcome.None;

            case JsonTokenType.Number:
                return reader.TryGetInt32(out var number) && Enum.IsDefined((DispositionOutcome)number)
                    ? (DispositionOutcome)number
                    : DispositionOutcome.None;

            default:
                reader.Skip();

                return DispositionOutcome.None;
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, DispositionOutcome value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString());
}
