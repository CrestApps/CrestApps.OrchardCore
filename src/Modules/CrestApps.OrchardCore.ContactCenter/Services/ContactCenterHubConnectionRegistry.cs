using Microsoft.AspNetCore.SignalR;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Tracks tenant-local Contact Center hub connections so feature disable can abort and drain them.
/// </summary>
public sealed class ContactCenterHubConnectionRegistry
{
    private readonly Dictionary<string, HubCallerContext> _connections = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();
    private bool _quiescing;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterHubConnectionRegistry"/> class.
    /// </summary>
    public ContactCenterHubConnectionRegistry()
    {
    }

    /// <summary>
    /// Registers a hub connection unless the Real-Time feature is quiescing.
    /// </summary>
    /// <param name="context">The hub caller context.</param>
    /// <returns><see langword="true"/> when the connection was registered; otherwise, <see langword="false"/>.</returns>
    public bool Register(HubCallerContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        lock (_lock)
        {
            if (_quiescing)
            {
                context.Abort();

                return false;
            }

            _connections[context.ConnectionId] = context;

            return true;
        }
    }

    /// <summary>
    /// Removes a disconnected hub connection.
    /// </summary>
    /// <param name="connectionId">The SignalR connection identifier.</param>
    public void Unregister(string connectionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionId);

        lock (_lock)
        {
            _connections.Remove(connectionId);
        }
    }

    /// <summary>
    /// Rejects new registrations and aborts current hub connections.
    /// </summary>
    public void Quiesce()
    {
        HubCallerContext[] connections;

        lock (_lock)
        {
            _quiescing = true;
            connections = [.. _connections.Values];
        }

        foreach (var connection in connections)
        {
            connection.Abort();
        }
    }

    /// <summary>
    /// Reopens hub connection registration after lifecycle recovery.
    /// </summary>
    public void Activate()
    {
        lock (_lock)
        {
            _quiescing = false;
        }
    }
}
