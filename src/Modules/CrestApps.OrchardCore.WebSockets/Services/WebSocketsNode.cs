namespace CrestApps.OrchardCore.WebSockets.Services;

/// <summary>
/// This process's identity, used to record which node holds a rendezvous.
/// </summary>
/// <remarks>
/// Generated per process rather than taken from the machine name: several nodes routinely run on one host (in
/// containers, or side by side during a rolling deployment), and two of them sharing an identity would make a
/// misrouted callback look like it had arrived in the right place.
/// </remarks>
internal static class WebSocketsNode
{
    /// <summary>
    /// Gets this process's node identity.
    /// </summary>
    public static string Id { get; } = Guid.NewGuid().ToString("n");
}
