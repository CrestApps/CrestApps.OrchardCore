using CrestApps.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Records predictability pay for each change the employer makes to a published shift inside the notice window of
/// the worker's rule set: the added-shift hours for a shift added or given to a worker, the moved-shift hours for a
/// moved one, and a share of the hours for one removed or taken from them. Changes a worker asked for owe nothing.
/// </summary>
internal sealed class PredictabilityPayChangeHandler : IScheduleChangeHandler
{
    private readonly WorkerStore _workerStore;
    private readonly PredictabilityPayStore _store;
    private readonly ILaborRuleEvaluator _evaluator;
    private readonly IClock _clock;

    public PredictabilityPayChangeHandler(WorkerStore workerStore, PredictabilityPayStore store, ILaborRuleEvaluator evaluator, IClock clock)
    {
        _workerStore = workerStore;
        _store = store;
        _evaluator = evaluator;
        _clock = clock;
    }

    public async Task RecordedAsync(ScheduleChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        if (change.InitiatedBy != ScheduleChangeInitiator.Employer)
        {
            return;
        }

        (string WorkerId, ScheduleChangeKind Kind)[] parts = change.Kind switch
        {
            ScheduleChangeKind.Added => [(change.WorkerId, ScheduleChangeKind.Added)],
            ScheduleChangeKind.Moved => [(change.WorkerId, ScheduleChangeKind.Moved)],
            ScheduleChangeKind.Removed => [(change.WorkerId, ScheduleChangeKind.Removed)],

            // The worker who gets the shift has one added; the one who loses it has one removed.
            ScheduleChangeKind.Reassigned => [(change.WorkerId, ScheduleChangeKind.Added), (change.PreviousWorkerId, ScheduleChangeKind.Removed)],
            _ => [],
        };

        if (parts.Length == 0)
        {
            return;
        }

        var existing = await _store.ListForChangeAsync(change.ItemId, cancellationToken);
        var starts = new[] { change.BeforeStartUtc, change.AfterStartUtc }.Where(value => value.HasValue).Select(value => value.Value).ToArray();

        if (starts.Length == 0)
        {
            return;
        }

        var shiftStart = starts.Min();
        var changedUtc = change.ChangedUtc == default ? _clock.UtcNow : change.ChangedUtc;
        var noticeHours = (decimal)(shiftStart - changedUtc).TotalHours;

        foreach (var (workerId, kind) in parts)
        {
            if (string.IsNullOrEmpty(workerId) || existing.Any(entry => entry.WorkerId == workerId))
            {
                continue;
            }

            var worker = await _workerStore.FindByIdAsync(workerId, cancellationToken);
            var set = worker is null ? null : await _evaluator.ResolveRuleSetAsync(worker, change.TeamId, cancellationToken);
            var rule = set?.Rules.FirstOrDefault(item => item.RuleKey == LaborRuleKeys.PredictabilityPay);

            if (rule is null || noticeHours >= rule.Number("NoticeDays", 14) * 24)
            {
                continue;
            }

            var hours = kind switch
            {
                ScheduleChangeKind.Added => rule.Number("AddedHours", 1),
                ScheduleChangeKind.Moved => rule.Number("MovedHours", 1),
                _ => Math.Round(ShiftHours(change) * rule.Number("RemovedPercent", 50) / 100m, 2),
            };

            if (hours <= 0)
            {
                continue;
            }

            await _store.CreateAsync(new PredictabilityPayEntry
            {
                ItemId = UniqueId.GenerateId(),
                WorkerId = workerId,
                TeamId = change.TeamId,
                PeriodId = change.PeriodId,
                ScheduleChangeId = change.ItemId,
                ShiftKey = change.ShiftKey,
                ChangeKind = kind,
                ShiftStartUtc = shiftStart,
                NoticeHours = Math.Round(Math.Max(0, noticeHours), 1),
                Hours = hours,
                RuleSetId = set.ItemId,
                CreatedUtc = _clock.UtcNow,
            }, cancellationToken);
        }
    }

    private static decimal ShiftHours(ScheduleChange change)
    {
        var start = change.BeforeStartUtc ?? change.AfterStartUtc;
        var end = change.BeforeEndUtc ?? change.AfterEndUtc;

        return start.HasValue && end.HasValue ? (decimal)(end.Value - start.Value).TotalHours : 0;
    }
}
