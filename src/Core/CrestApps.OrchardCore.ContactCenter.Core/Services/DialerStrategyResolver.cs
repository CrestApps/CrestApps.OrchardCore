using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Resolves a registered <see cref="IDialerStrategy"/> by dialing mode. Modes without a registered
/// strategy (such as the blocked Predictive mode) resolve to <see langword="null"/> so the caller can
/// safely refuse to dial.
/// </summary>
public sealed class DialerStrategyResolver : IDialerStrategyResolver
{
    private readonly IEnumerable<IDialerStrategy> _strategies;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerStrategyResolver"/> class.
    /// </summary>
    /// <param name="strategies">The registered dialing strategies.</param>
    public DialerStrategyResolver(IEnumerable<IDialerStrategy> strategies)
    {
        _strategies = strategies;
    }

    /// <inheritdoc/>
    public IDialerStrategy Resolve(DialerMode mode)
    {
        return _strategies.FirstOrDefault(strategy => strategy.Mode == mode);
    }
}
