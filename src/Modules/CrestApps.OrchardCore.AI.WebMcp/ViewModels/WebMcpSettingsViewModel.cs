namespace CrestApps.OrchardCore.AI.WebMcp.ViewModels;

/// <summary>
/// The editable WebMCP settings on the AI settings page.
/// </summary>
public class WebMcpSettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether every selectable tool and tool instance is given to browser agents.
    /// </summary>
    public bool ExposeAllTools { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether admin pages offer WebMCP tools.
    /// </summary>
    public bool EnableOnAdminPages { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether front-end pages offer WebMCP tools.
    /// </summary>
    public bool EnableOnFrontEndPages { get; set; }
}
