using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.ThemeDesigner.Models;

/// <summary>
/// Which side of the site a template belongs to.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ThemeDesignScope>))]
public enum ThemeDesignScope
{
    /// <summary>
    /// The front end, through Orchard Core's Templates feature.
    /// </summary>
    Site,

    /// <summary>
    /// The admin, through Orchard Core's Admin Templates feature.
    /// </summary>
    Admin,
}
