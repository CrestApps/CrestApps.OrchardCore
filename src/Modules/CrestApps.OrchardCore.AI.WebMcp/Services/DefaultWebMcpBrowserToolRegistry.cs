using System.Collections.Concurrent;
using CrestApps.OrchardCore.AI.WebMcp.Models;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Keeps the page tools of each user's last reporting tab in memory, for the tenant.
/// </summary>
public sealed class DefaultWebMcpBrowserToolRegistry : IWebMcpBrowserToolRegistry
{
    private readonly ConcurrentDictionary<string, WebMcpBrowserToolRegistration> _registrations = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public void Register(string userId, string connectionId, IReadOnlyList<WebMcpBrowserTool> tools)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        ArgumentException.ThrowIfNullOrEmpty(connectionId);
        ArgumentNullException.ThrowIfNull(tools);

        _registrations[userId] = new WebMcpBrowserToolRegistration
        {
            UserId = userId,
            ConnectionId = connectionId,
            Tools = tools,
        };
    }

    /// <inheritdoc/>
    public bool Remove(string userId, string connectionId)
    {
        if (string.IsNullOrEmpty(userId) ||
            !_registrations.TryGetValue(userId, out var registration) ||
            !string.Equals(registration.ConnectionId, connectionId, StringComparison.Ordinal))
        {
            return false;
        }

        return _registrations.TryRemove(new KeyValuePair<string, WebMcpBrowserToolRegistration>(userId, registration));
    }

    /// <inheritdoc/>
    public void RemoveConnection(string connectionId)
    {
        foreach (var entry in _registrations)
        {
            if (string.Equals(entry.Value.ConnectionId, connectionId, StringComparison.Ordinal))
            {
                _registrations.TryRemove(entry);
            }
        }
    }

    /// <inheritdoc/>
    public bool TryGet(string userId, out WebMcpBrowserToolRegistration registration)
    {
        if (string.IsNullOrEmpty(userId))
        {
            registration = null;

            return false;
        }

        return _registrations.TryGetValue(userId, out registration);
    }
}
