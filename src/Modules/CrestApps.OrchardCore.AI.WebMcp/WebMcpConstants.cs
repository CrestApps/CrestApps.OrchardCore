using CoreWebMcpConstants = CrestApps.Core.AI.WebMcp.WebMcpConstants;

namespace CrestApps.OrchardCore.AI.WebMcp;

/// <summary>
/// Provides the constants of the WebMCP module.
/// </summary>
public static class WebMcpConstants
{
    /// <summary>
    /// The relative route of the endpoint that lists the tools the current user may call.
    /// </summary>
    public const string ToolsRoute = $"{CoreWebMcpConstants.DefaultRoutePrefix}/tools";

    /// <summary>
    /// The relative route of the endpoint that calls one tool.
    /// </summary>
    public const string InvokeRoute = $"{CoreWebMcpConstants.DefaultRoutePrefix}/tools/{{name}}/invoke";

    /// <summary>
    /// The id of the JSON block that carries the page configuration to the browser scripts.
    /// </summary>
    public const string ConfigurationElementId = CoreWebMcpConstants.ConfigurationElementId;

    /// <summary>
    /// The shell configuration section that can override the WebMCP settings.
    /// </summary>
    public const string ConfigurationSection = "CrestApps:AI:WebMcp";

    /// <summary>
    /// The feature identifiers of the module.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// The WebMCP Tools feature.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.AI.WebMcp";

        /// <summary>
        /// The WebMCP Page Tools feature.
        /// </summary>
        public const string PageTools = "CrestApps.OrchardCore.AI.WebMcp.PageTools";

        /// <summary>
        /// The WebMCP Chat Bridge feature.
        /// </summary>
        public const string Chat = "CrestApps.OrchardCore.AI.WebMcp.Chat";
    }

    /// <summary>
    /// The names of the module's resources.
    /// </summary>
    public static class ResourceNames
    {
        /// <summary>
        /// The W3C reference polyfill that installs <c>document.modelContext</c> in browsers without native WebMCP.
        /// </summary>
        public const string Polyfill = "CrestAppsWebMcpPolyfill";

        /// <summary>
        /// The script that registers the site's AI tools with <c>document.modelContext</c> and connects the AI chat to
        /// the page's tools.
        /// </summary>
        public const string Script = "CrestAppsWebMcp";

        /// <summary>
        /// The page tools script.
        /// </summary>
        public const string PageToolsScript = "CrestAppsWebMcpPageTools";
    }
}
