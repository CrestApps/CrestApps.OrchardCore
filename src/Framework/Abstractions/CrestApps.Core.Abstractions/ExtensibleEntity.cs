using System.Text.Json.Serialization;

namespace CrestApps.Core;

/// <summary>
/// Base class for entities that support dynamic extensible properties.
/// </summary>
public abstract class ExtensibleEntity
{
    [JsonExtensionData]
    public IDictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
}
