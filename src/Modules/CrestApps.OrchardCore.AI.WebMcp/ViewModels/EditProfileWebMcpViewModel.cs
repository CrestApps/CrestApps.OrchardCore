namespace CrestApps.OrchardCore.AI.WebMcp.ViewModels;

/// <summary>
/// The WebMCP settings of an AI profile in the profile editor.
/// </summary>
public class EditProfileWebMcpViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the profile's chat may call the WebMCP tools on the user's page.
    /// </summary>
    public bool AllowPageTools { get; set; }
}
