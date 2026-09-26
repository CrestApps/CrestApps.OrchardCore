namespace CrestApps.OrchardCore.WebSockets;

/// <summary>
/// Contains constant values for the WebSockets module.
/// </summary>
public static class WebSocketsConstants
{
    /// <summary>
    /// The shell configuration section bound onto the ASP.NET Core <c>WebSocketOptions</c> the middleware consumes
    /// (for example <c>KeepAliveInterval</c> and <c>AllowedOrigins</c>).
    /// </summary>
    public const string ConfigurationSectionPath = "CrestApps:WebSockets";

    /// <summary>
    /// Contains the feature identifiers exposed by the WebSockets module.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// The identifier of the WebSockets feature. It is enabled by dependency only: a feature that hosts a raw
        /// WebSocket endpoint depends on it so the ASP.NET Core WebSocket middleware is present in the tenant
        /// pipeline.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.WebSockets";
    }
}
