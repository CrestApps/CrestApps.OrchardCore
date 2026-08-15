namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Resolves a registered <see cref="ITaxSourcingStrategy"/> by name.
/// </summary>
public interface ITaxSourcingStrategyProvider
{
    /// <summary>
    /// Gets the sourcing strategy registered under the supplied name.
    /// </summary>
    /// <param name="name">The name of the sourcing strategy.</param>
    /// <returns>The matching sourcing strategy, or <see langword="null"/> when none is registered.</returns>
    ITaxSourcingStrategy GetStrategy(string name);
}
