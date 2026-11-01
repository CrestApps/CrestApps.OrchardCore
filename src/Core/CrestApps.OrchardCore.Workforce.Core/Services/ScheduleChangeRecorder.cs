using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Records a change to a published schedule and tells the <see cref="IScheduleChangeHandler"/>s.
/// </summary>
internal sealed class ScheduleChangeRecorder
{
    private readonly ScheduleChangeStore _store;
    private readonly IEnumerable<IScheduleChangeHandler> _handlers;

    public ScheduleChangeRecorder(ScheduleChangeStore store, IEnumerable<IScheduleChangeHandler> handlers)
    {
        _store = store;
        _handlers = handlers;
    }

    public async Task RecordAsync(ScheduleChange change, CancellationToken cancellationToken = default)
    {
        await _store.CreateAsync(change, cancellationToken);

        foreach (var handler in _handlers)
        {
            await handler.RecordedAsync(change, cancellationToken);
        }
    }
}
