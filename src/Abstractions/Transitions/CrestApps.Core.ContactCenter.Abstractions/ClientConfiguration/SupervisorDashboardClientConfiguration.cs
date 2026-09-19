namespace CrestApps.Core.ContactCenter.ClientConfiguration;

/// <summary>
/// What the supervisor dashboard in the browser is told about the server it is talking to.
/// </summary>
/// <remarks>
/// Serialized camel-cased into the page and read once on load.
/// </remarks>
public sealed class SupervisorDashboardClientConfiguration
{
    /// <summary>
    /// Gets or sets the URL of the Contact Center hub.
    /// </summary>
    public string HubUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL the dashboard polls for the floor snapshot.
    /// </summary>
    public string StateUrl { get; set; }

    /// <summary>
    /// Gets or sets the URL that monitors, whispers to, or joins a live call.
    /// </summary>
    /// <remarks>
    /// The dashboard hides its engagement controls when this is absent rather than showing controls
    /// that fail, so a host that cannot resolve it removes the capability quietly.
    /// </remarks>
    public string EngageUrl { get; set; }

    /// <summary>
    /// Gets or sets the request token the browser sends back on every post.
    /// </summary>
    public string AntiForgeryToken { get; set; }

    /// <summary>
    /// Gets or sets the translated strings the dashboard renders.
    /// </summary>
    /// <remarks>
    /// Filled by the host rather than by this suite. Translations are keyed on where the string was
    /// written as much as on the string itself, so moving these out of the host's own templates would
    /// leave every existing translation unreachable.
    /// </remarks>
    public IDictionary<string, string> Strings { get; set; }
}
