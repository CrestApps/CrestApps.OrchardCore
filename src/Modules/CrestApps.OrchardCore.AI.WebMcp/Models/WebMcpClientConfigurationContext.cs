using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.AI.WebMcp.Models;

/// <summary>
/// The WebMCP configuration of a page while it is being built.
/// </summary>
public sealed class WebMcpClientConfigurationContext
{
    private readonly List<string> _scripts = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="WebMcpClientConfigurationContext"/> class.
    /// </summary>
    /// <param name="httpContext">The HTTP context of the page.</param>
    /// <param name="isAdminPage">Whether the page is an admin page.</param>
    public WebMcpClientConfigurationContext(HttpContext httpContext, bool isAdminPage)
    {
        HttpContext = httpContext;
        IsAdminPage = isAdminPage;
    }

    /// <summary>
    /// Gets the HTTP context of the page.
    /// </summary>
    public HttpContext HttpContext { get; }

    /// <summary>
    /// Gets a value indicating whether the page is an admin page.
    /// </summary>
    public bool IsAdminPage { get; }

    /// <summary>
    /// Gets the names of the script resources the page loads after the core WebMCP script, in order.
    /// </summary>
    public IReadOnlyList<string> Scripts => _scripts;

    /// <summary>
    /// Gets the settings written to the page's configuration block, which the scripts read as
    /// <c>window.crestAppsWebMcp.config</c>.
    /// </summary>
    public IDictionary<string, object> Settings { get; } = new Dictionary<string, object>(StringComparer.Ordinal);

    /// <summary>
    /// Adds a script resource the page loads after the core WebMCP script. A script added twice loads once.
    /// </summary>
    /// <param name="resourceName">The name of the script resource.</param>
    public void AddScript(string resourceName)
    {
        ArgumentException.ThrowIfNullOrEmpty(resourceName);

        if (!_scripts.Contains(resourceName, StringComparer.Ordinal))
        {
            _scripts.Add(resourceName);
        }
    }
}
