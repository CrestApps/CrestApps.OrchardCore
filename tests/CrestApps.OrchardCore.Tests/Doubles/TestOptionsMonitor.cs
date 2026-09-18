using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// A minimal <see cref="IOptionsMonitor{TOptions}"/> that always returns a fixed value, for
/// constructing a service that observes its configuration through a monitor.
/// </summary>
internal sealed class TestOptionsMonitor<T> : IOptionsMonitor<T>
{
    public TestOptionsMonitor(T value)
        => CurrentValue = value;

    public T CurrentValue { get; }

    public T Get(string name)
        => CurrentValue;

    public IDisposable OnChange(Action<T, string> listener)
        => null;
}
