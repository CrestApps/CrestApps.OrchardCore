using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The <see cref="IWorkforceEvents"/> used when nothing listens: it does nothing.
/// </summary>
internal sealed class NullWorkforceEvents : IWorkforceEvents
{
    public Task RaiseAsync(string name, IDictionary<string, object> input, string correlationId, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
