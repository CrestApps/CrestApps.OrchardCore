using System.Security.Claims;
using CrestApps.OrchardCore.AI.WebMcp.Models;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Lists and calls the AI tools a user may give to browser agents through WebMCP.
/// </summary>
public interface IWebMcpToolService
{
    /// <summary>
    /// Lists the tools the user may call: selectable tools and tool instances that the WebMCP settings allow and
    /// that the user is authorized to use.
    /// </summary>
    /// <param name="user">The signed-in user.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tools, by the function name the agent calls them by.</returns>
    Task<IReadOnlyList<WebMcpToolDescriptor>> GetToolsAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Calls one of the tools <see cref="GetToolsAsync"/> lists for the user.
    /// </summary>
    /// <param name="user">The signed-in user.</param>
    /// <param name="name">The function name of the tool.</param>
    /// <param name="arguments">The tool's arguments, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The tool's answer; <see cref="WebMcpToolInvocationStatus.NotFound"/> when the tool is not listed for the user,
    /// whatever the reason; or <see cref="WebMcpToolInvocationStatus.Failed"/>, without the error details, when it throws.
    /// </returns>
    Task<WebMcpToolInvocationResult> InvokeAsync(
        ClaimsPrincipal user,
        string name,
        IDictionary<string, object> arguments,
        CancellationToken cancellationToken = default);
}
