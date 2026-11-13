using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.SiteResources.Models;

/// <summary>
/// Where in the page a script is added. Stylesheets always go in the head.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SiteResourceLocation>))]
public enum SiteResourceLocation
{
    /// <summary>
    /// At the end of the body, after the page's content.
    /// </summary>
    Foot,

    /// <summary>
    /// In the head, before the page's content.
    /// </summary>
    Head,
}
