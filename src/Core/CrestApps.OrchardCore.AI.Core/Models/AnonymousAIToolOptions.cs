namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// The AI tools marked safe for visitors who are not signed in, such as an in-browser agent on the storefront. A
/// surface that serves anonymous callers, such as WebMCP's storefront tools, offers only these tools, and only when
/// the site turns it on.
/// </summary>
/// <remarks>
/// Mark a tool with <see cref="Extensions.AnonymousAIToolServiceCollectionExtensions.MarkAIToolAnonymousSafe"/> only
/// when anyone may run it: it reads public data or acts on the caller's own data (such as their own cart), never
/// reveals another person's data, and never pays, deletes or changes the site.
/// </remarks>
public sealed class AnonymousAIToolOptions
{
    private readonly HashSet<string> _tools = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the names of the tools marked safe for anonymous callers. Names are matched without regard to case, as
    /// the tool catalog matches them.
    /// </summary>
    public IReadOnlyCollection<string> Tools => _tools;

    /// <summary>
    /// Marks a tool safe for anonymous callers.
    /// </summary>
    /// <param name="toolName">The name the tool is registered under.</param>
    public void MarkSafe(string toolName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        _tools.Add(toolName.Trim());
    }

    /// <summary>
    /// Determines whether a tool is marked safe for anonymous callers.
    /// </summary>
    /// <param name="toolName">The name the tool is registered under.</param>
    /// <returns><see langword="true"/> when the tool is marked; otherwise <see langword="false"/>.</returns>
    public bool IsSafe(string toolName)
        => !string.IsNullOrWhiteSpace(toolName) && _tools.Contains(toolName);
}
