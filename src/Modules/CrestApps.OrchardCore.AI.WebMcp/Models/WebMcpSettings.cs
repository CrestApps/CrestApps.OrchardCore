namespace CrestApps.OrchardCore.AI.WebMcp.Models;

/// <summary>
/// The site settings of the WebMCP Tools feature. <see cref="Services.WebMcpOptionsConfiguration"/> loads them into
/// <see cref="WebMcpOptions"/>.
/// </summary>
public sealed class WebMcpSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether every selectable tool and tool instance is given to browser agents.
    /// When <see langword="false"/> (the default), only the tools in <see cref="Tools"/> are given.
    /// </summary>
    public bool ExposeAllTools { get; set; }

    /// <summary>
    /// Gets or sets the names of the tools and tool instances given to browser agents when
    /// <see cref="ExposeAllTools"/> is <see langword="false"/>.
    /// </summary>
    public IList<string> Tools { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether admin pages offer WebMCP tools. The default is <see langword="true"/>.
    /// </summary>
    public bool EnableOnAdminPages { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether front-end pages offer WebMCP tools. The default is <see langword="false"/>.
    /// </summary>
    public bool EnableOnFrontEndPages { get; set; }
}
