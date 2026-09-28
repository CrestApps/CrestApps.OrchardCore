using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Telephony.Doubles;

/// <summary>
/// A minimal <see cref="IOptionsMonitor{TOptions}"/> that always returns a fixed value, used by
/// telephony tests to construct services that now observe their options through a monitor.
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
