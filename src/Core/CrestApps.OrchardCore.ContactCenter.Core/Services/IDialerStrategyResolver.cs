using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Resolves the registered <see cref="IDialerStrategy"/> for a dialing mode.
/// </summary>
public interface IDialerStrategyResolver
{
    /// <summary>
    /// Resolves the strategy that implements the specified dialing mode.
    /// </summary>
    /// <param name="mode">The dialing mode to resolve.</param>
    /// <returns>The matching strategy, or <see langword="null"/> when no automated strategy supports the mode.</returns>
    IDialerStrategy Resolve(DialerMode mode);
}
