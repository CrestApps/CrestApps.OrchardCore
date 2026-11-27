using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.ThemeDesigner.Models;

/// <summary>
/// What a change in a theme draft is about.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ThemeDesignChangeKind>))]
public enum ThemeDesignChangeKind
{
    /// <summary>
    /// A front-end shape template.
    /// </summary>
    SiteTemplate,

    /// <summary>
    /// An admin shape template.
    /// </summary>
    AdminTemplate,

    /// <summary>
    /// A site resource (a stylesheet or a script).
    /// </summary>
    SiteResource,
}
