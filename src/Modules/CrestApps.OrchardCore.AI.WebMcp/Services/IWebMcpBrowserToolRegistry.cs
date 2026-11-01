using CrestApps.OrchardCore.AI.WebMcp.Models;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Remembers, for each user, the page tools of the browser tab that reported them last.
/// </summary>
public interface IWebMcpBrowserToolRegistry
{
    /// <summary>
    /// Records the page tools a user's tab offers, in place of what the user's tabs reported before.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="connectionId">The SignalR connection of the tab.</param>
    /// <param name="tools">The checked tools.</param>
    void Register(string userId, string connectionId, IReadOnlyList<WebMcpBrowserTool> tools);

    /// <summary>
    /// Forgets a user's page tools when that connection reported them last.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="connectionId">The SignalR connection of the tab.</param>
    /// <returns><see langword="true"/> when the entry was removed; otherwise <see langword="false"/>.</returns>
    bool Remove(string userId, string connectionId);

    /// <summary>
    /// Forgets every entry a closed connection reported.
    /// </summary>
    /// <param name="connectionId">The SignalR connection.</param>
    void RemoveConnection(string connectionId);

    /// <summary>
    /// Finds the page tools a user's tab reported last.
    /// </summary>
    /// <param name="userId">The identifier of the user.</param>
    /// <param name="registration">The entry, when there is one.</param>
    /// <returns><see langword="true"/> when the user has an entry; otherwise <see langword="false"/>.</returns>
    bool TryGet(string userId, out WebMcpBrowserToolRegistration registration);
}
