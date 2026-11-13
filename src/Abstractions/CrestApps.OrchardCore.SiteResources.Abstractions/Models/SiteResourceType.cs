using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.SiteResources.Models;

/// <summary>
/// What a site resource holds.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SiteResourceType>))]
public enum SiteResourceType
{
    /// <summary>
    /// A CSS stylesheet.
    /// </summary>
    Stylesheet,

    /// <summary>
    /// A JavaScript script.
    /// </summary>
    Script,
}
