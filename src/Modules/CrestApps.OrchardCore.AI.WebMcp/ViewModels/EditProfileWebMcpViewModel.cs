namespace CrestApps.OrchardCore.AI.WebMcp.ViewModels;

/// <summary>
/// The WebMCP settings of an AI profile in the profile editor.
/// </summary>
public class EditProfileWebMcpViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the profile's chat may discover and run the WebMCP tools the user's
    /// page exposes.
    /// </summary>
    public bool AllowPageTools { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the profile's chat may also run the page tools marked with the WebMCP
    /// <c>consequentialHint</c> annotation, such as placing an order.
    /// </summary>
    public bool AllowConsequentialPageTools { get; set; }
}
