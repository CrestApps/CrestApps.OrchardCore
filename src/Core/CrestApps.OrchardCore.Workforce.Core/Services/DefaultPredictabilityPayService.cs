using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="IPredictabilityPayService"/>.
/// </summary>
internal sealed class DefaultPredictabilityPayService : IPredictabilityPayService
{
    private readonly PredictabilityPayStore _store;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public DefaultPredictabilityPayService(PredictabilityPayStore store, IClock clock, IStringLocalizer<DefaultPredictabilityPayService> stringLocalizer)
    {
        _store = store;
        _clock = clock;
        S = stringLocalizer;
    }

    public Task<(int Count, IReadOnlyList<PredictabilityPayEntry> Items)> PageAsync(PredictabilityPayQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _store.PageAsync(query, skip, take, cancellationToken);
    }

    public async Task<PredictabilityPayEntry> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        return string.IsNullOrEmpty(id) ? null : await _store.FindByIdAsync(id, cancellationToken);
    }

    public Task<PlanningResult<PredictabilityPayEntry>> ApproveAsync(string id, string actor, CancellationToken cancellationToken = default)
    {
        return DecideAsync(id, PredictabilityPayStatus.Approved, null, actor, cancellationToken);
    }

    public Task<PlanningResult<PredictabilityPayEntry>> WaiveAsync(string id, string reason, string actor, CancellationToken cancellationToken = default)
    {
        return DecideAsync(id, PredictabilityPayStatus.Waived, reason, actor, cancellationToken);
    }

    private async Task<PlanningResult<PredictabilityPayEntry>> DecideAsync(string id, PredictabilityPayStatus status, string note, string actor, CancellationToken cancellationToken)
    {
        var result = new PlanningResult<PredictabilityPayEntry>();
        var entry = await FindAsync(id, cancellationToken);

        if (entry is null)
        {
            result.Fail(S["The predictability pay entry doesn't exist."]);

            return result;
        }

        if (entry.Status != PredictabilityPayStatus.Pending)
        {
            result.Fail(S["This entry has already been decided."]);

            return result;
        }

        if (status == PredictabilityPayStatus.Waived && string.IsNullOrWhiteSpace(note))
        {
            result.Fail(S["Give the reason it isn't owed."]);

            return result;
        }

        entry.Status = status;
        entry.Note = string.IsNullOrWhiteSpace(note) ? entry.Note : note.Trim();
        entry.DecidedBy = actor;
        entry.DecidedUtc = _clock.UtcNow;
        await _store.UpdateAsync(entry, cancellationToken);
        result.Value = entry;

        return result;
    }
}
