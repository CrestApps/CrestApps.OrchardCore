using System.Text.Json.Serialization;

namespace CrestApps;

/// <summary>
/// Base class for entities that support dynamic extensible properties.
/// </summary>
public class ExtensibleEntity
{
    [JsonExtensionData]
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
}
