namespace CrestApps.OrchardCore.AI.WebMcp.Models;

/// <summary>
/// Which pages get the WebMCP scripts: the stored <see cref="WebMcpSettings"/>, overridden by the
/// <c>CrestApps:AI:WebMcp</c> shell configuration section. The tools given to browser agents are in CrestApps.Core's
/// <see cref="CrestApps.Core.AI.WebMcp.Models.WebMcpOptions"/>.
/// </summary>
public sealed class WebMcpPageOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether admin pages get the WebMCP scripts.
    /// </summary>
    public bool EnableOnAdminPages { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether front-end pages get the WebMCP scripts.
    /// </summary>
    public bool EnableOnFrontEndPages { get; set; }

    /// <summary>
    /// Gets or sets the origins of cross-origin frames whose shared WebMCP tools the AI chat may use. A frame also
    /// needs <c>allow="tools"</c>. Empty by default, so the chat only uses the page's own tools.
    /// </summary>
    public IList<string> PageToolOrigins { get; set; } = [];

    /// <summary>
    /// Determines whether a page of the given kind gets the WebMCP scripts.
    /// </summary>
    /// <param name="isAdminPage">Whether the page is an admin page.</param>
    /// <returns><see langword="true"/> when the page kind is turned on; otherwise <see langword="false"/>.</returns>
    public bool IsEnabledFor(bool isAdminPage)
    {
        return isAdminPage
            ? EnableOnAdminPages
            : EnableOnFrontEndPages;
    }
}
