namespace CrestApps.OrchardCore;

/// <summary>
/// Where the <c>@crestapps/ai-chat-ui</c> package's scripts and styles are served from, so every resource manifest
/// pins the same version.
/// </summary>
public static class AIChatUiCdn
{
    /// <summary>
    /// The version of <c>@crestapps/ai-chat-ui</c> the resource manifests use.
    /// </summary>
    public const string Version = "2.0.0-preview.258";

    /// <summary>
    /// The URL of the package's <c>dist</c> folder, ending with a slash.
    /// </summary>
    public const string DistUrl = "https://cdn.jsdelivr.net/npm/@crestapps/ai-chat-ui@" + Version + "/dist/";
}
