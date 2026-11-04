using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The limits from the shift planning options and the worker's own hours.
/// </summary>
internal sealed class DefaultWorkerScheduleLimitsProvider : IWorkerScheduleLimitsProvider
{
    private readonly ShiftPlanningOptions _options;

    public DefaultWorkerScheduleLimitsProvider(IOptions<ShiftPlanningOptions> options)
    {
        _options = options.Value;
    }

    public Task<WorkerScheduleLimits> GetAsync(WorkerProfile worker, string teamId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        return Task.FromResult(new WorkerScheduleLimits
        {
            MinRestHours = Math.Max(0, _options.MinRestHours),
            MaxConsecutiveDays = Math.Max(1, _options.MaxConsecutiveDays),
            MaxHoursPerWeek = worker.MaxHoursPerWeek,
            MaxHoursPerDay = worker.MaxHoursPerDay,
        });
    }
}
