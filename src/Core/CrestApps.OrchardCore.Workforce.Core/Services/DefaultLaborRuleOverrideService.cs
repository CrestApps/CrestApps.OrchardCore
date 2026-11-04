using CrestApps.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="ILaborRuleOverrideService"/>.
/// </summary>
internal sealed class DefaultLaborRuleOverrideService : ILaborRuleOverrideService
{
    private readonly LaborRuleOverrideStore _store;
    private readonly IClock _clock;

    public DefaultLaborRuleOverrideService(LaborRuleOverrideStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    public async Task<IReadOnlyList<LaborRuleOverride>> RecordAsync(IEnumerable<LaborRuleViolation> violations, string reason, string source, string periodId, string teamId, string actor, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var records = new List<LaborRuleOverride>();

        foreach (var violation in violations ?? [])
        {
            var record = new LaborRuleOverride
            {
                ItemId = UniqueId.GenerateId(),
                WorkerId = violation.WorkerId,
                TeamId = teamId,
                RuleSetId = violation.RuleSetId,
                RuleKey = violation.RuleKey,
                Message = violation.Message,
                Reason = reason?.Trim(),
                Source = source,
                PeriodId = periodId,
                ShiftKeys = violation.ShiftKeys.ToList(),
                Actor = actor,
                CreatedUtc = now,
            };

            await _store.CreateAsync(record, cancellationToken);
            records.Add(record);
        }

        return records;
    }

    public Task<(int Count, IReadOnlyList<LaborRuleOverride> Items)> PageAsync(IReadOnlyCollection<string> teamIds, int skip, int take, CancellationToken cancellationToken = default)
    {
        return _store.PageAsync(teamIds, skip, take, cancellationToken);
    }
}
