using Microsoft.Extensions.Primitives;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Signals that a piece of cached Contact Center configuration has changed, and hands out the change
/// tokens that let a cache notice.
/// </summary>
/// <remarks>
/// A host that runs more than one process implements this over whatever it uses to reach the other
/// processes, so a configuration change made on one node is honoured on all of them.
/// </remarks>
public interface IContactCenterConfigurationChangeNotifier
{
    /// <summary>
    /// Gets a change token that is tripped when the key is next notified.
    /// </summary>
    /// <param name="key">The key identifying the cached configuration.</param>
    /// <returns>The change token.</returns>
    IChangeToken Watch(string key);

    /// <summary>
    /// Marks the key as changed, tripping every token handed out for it.
    /// </summary>
    /// <param name="key">The key identifying the cached configuration.</param>
    Task NotifyChangedAsync(string key);
}
