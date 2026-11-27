using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.ThemeDesigner.Models;

/// <summary>
/// What publishing a change in a theme draft does to the published item.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ThemeDesignChangeStatus>))]
public enum ThemeDesignChangeStatus
{
    /// <summary>
    /// The item is not published yet; publishing adds it.
    /// </summary>
    Added,

    /// <summary>
    /// The item is published; publishing replaces it.
    /// </summary>
    Changed,

    /// <summary>
    /// The item is published; publishing removes it.
    /// </summary>
    Removed,
}
