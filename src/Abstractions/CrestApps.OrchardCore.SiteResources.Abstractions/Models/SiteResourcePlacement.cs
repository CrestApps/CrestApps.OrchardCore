using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.SiteResources.Models;

/// <summary>
/// When a site resource is added to a page.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SiteResourcePlacement>))]
public enum SiteResourcePlacement
{
    /// <summary>
    /// On every page of its target.
    /// </summary>
    Global,

    /// <summary>
    /// Only on the pages whose templates ask for it by name, such as <c>{% script name: "my-slider" %}</c> in Liquid
    /// or <c>&lt;script asp-name="my-slider"&gt;</c> in Razor.
    /// </summary>
    OnDemand,
}
