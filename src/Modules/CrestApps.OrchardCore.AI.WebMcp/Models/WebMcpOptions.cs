namespace CrestApps.OrchardCore.AI.WebMcp.Models;

/// <summary>
/// The effective WebMCP settings: the stored <see cref="WebMcpSettings"/>, overridden by the
/// <c>CrestApps:AI:WebMcp</c> shell configuration section.
/// </summary>
public sealed class WebMcpOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether every selectable tool and tool instance is given to browser agents.
    /// </summary>
    public bool ExposeAllTools { get; set; }

    /// <summary>
    /// Gets or sets the names of the tools and tool instances given to browser agents when
    /// <see cref="ExposeAllTools"/> is <see langword="false"/>.
    /// </summary>
    public IList<string> Tools { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether admin pages offer WebMCP tools.
    /// </summary>
    public bool EnableOnAdminPages { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether front-end pages offer WebMCP tools.
    /// </summary>
    public bool EnableOnFrontEndPages { get; set; }

    /// <summary>
    /// Determines whether a page of the given kind offers WebMCP tools.
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
