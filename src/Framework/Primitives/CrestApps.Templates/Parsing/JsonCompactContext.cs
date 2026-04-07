using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrestApps.Templates.Parsing;

[JsonSerializable(typeof(JsonDocument))]
[JsonSourceGenerationOptions(WriteIndented = false)]
internal sealed partial class JsonCompactContext : JsonSerializerContext
{
}
