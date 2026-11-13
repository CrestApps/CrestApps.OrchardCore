using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.SiteResources.Models;

/// <summary>
/// The pages a site resource is offered to.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SiteResourceTarget>))]
public enum SiteResourceTarget
{
    /// <summary>
    /// The pages of the front end.
    /// </summary>
    Frontend,

    /// <summary>
    /// The pages of the admin.
    /// </summary>
    Admin,

    /// <summary>
    /// Every page, front end and admin.
    /// </summary>
    Everywhere,
}
