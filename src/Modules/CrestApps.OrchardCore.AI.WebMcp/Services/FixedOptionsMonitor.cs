using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// An options monitor that always answers the same value, used to give CrestApps.Core's tool service the allow-list
/// of one content item.
/// </summary>
/// <typeparam name="TOptions">The type of the options.</typeparam>
internal sealed class FixedOptionsMonitor<TOptions> : IOptionsMonitor<TOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FixedOptionsMonitor{TOptions}"/> class.
    /// </summary>
    /// <param name="value">The value.</param>
    public FixedOptionsMonitor(TOptions value)
    {
        CurrentValue = value;
    }

    /// <inheritdoc/>
    public TOptions CurrentValue { get; }

    /// <inheritdoc/>
    public TOptions Get(string name) => CurrentValue;

    /// <inheritdoc/>
    public IDisposable OnChange(Action<TOptions, string> listener) => null;
}
