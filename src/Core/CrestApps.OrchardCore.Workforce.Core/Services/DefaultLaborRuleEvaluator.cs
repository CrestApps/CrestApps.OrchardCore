using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// The default <see cref="ILaborRuleEvaluator"/>: it finds the worker's rule set, reads their published shifts and
/// approved time off around the shifts being checked, and runs each rule of the set.
/// </summary>
internal sealed class DefaultLaborRuleEvaluator : ILaborRuleEvaluator
{
    private readonly LaborRuleSetStore _setStore;
    private readonly TeamStore _teamStore;
    private readonly WorkerShiftReader _shiftReader;
    private readonly ActivityCodeStore _activityCodeStore;
    private readonly TeamTimeZoneResolver _timeZones;
    private readonly IEnumerable<ILaborRule> _rules;
    private readonly IClock _clock;

    public DefaultLaborRuleEvaluator(
        LaborRuleSetStore setStore,
        TeamStore teamStore,
        WorkerShiftReader shiftReader,
        ActivityCodeStore activityCodeStore,
        TeamTimeZoneResolver timeZones,
        IEnumerable<ILaborRule> rules,
        IClock clock)
    {
        _setStore = setStore;
        _teamStore = teamStore;
        _shiftReader = shiftReader;
        _activityCodeStore = activityCodeStore;
        _timeZones = timeZones;
        _rules = rules;
        _clock = clock;
    }

    public async Task<LaborRuleSet> ResolveRuleSetAsync(WorkerProfile worker, string teamId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(worker);

        var sets = (await _setStore.ListAsync(cancellationToken))
            .Where(set => set.Enabled && (set.EmploymentTypes.Count == 0 || set.EmploymentTypes.Contains(worker.EmploymentType)))
            .ToArray();

        if (sets.Length == 0)
        {
            return null;
        }

        var own = sets.FirstOrDefault(set => set.WorkerIds.Contains(worker.ItemId));

        if (own is not null)
        {
            return own;
        }

        var parents = (await _teamStore.ListAsync(cancellationToken)).ToDictionary(team => team.ItemId, team => team.ParentId, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var current = teamId; !string.IsNullOrEmpty(current) && seen.Add(current); current = parents.TryGetValue(current, out var parentId) ? parentId : null)
        {
            var match = sets.FirstOrDefault(set => set.TeamIds.Contains(current));

            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    public async Task<LaborRuleResult> EvaluateAsync(LaborRuleRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = new LaborRuleResult();
        var worker = request.Worker;

        if (worker is null || request.Candidates.Count == 0)
        {
            return result;
        }

        result.RuleSet = await ResolveRuleSetAsync(worker, request.TeamId, cancellationToken);

        if (result.RuleSet is null || result.RuleSet.Rules.Count == 0)
        {
            return result;
        }

        var timeZoneId = await _timeZones.GetAsync(request.TeamId, cancellationToken);
        var fromUtc = request.Candidates.Min(shift => shift.StartUtc).AddDays(-8);
        var toUtc = request.Candidates.Max(shift => shift.EndUtc).AddDays(8);
        var candidateKeys = new HashSet<string>(request.Candidates.Select(shift => shift.ShiftKey).Where(key => key is not null), StringComparer.Ordinal);
        var codes = (await _activityCodeStore.GetAllAsync(cancellationToken)).ToDictionary(code => code.ItemId, StringComparer.Ordinal);
        var shifts = new List<LaborRuleShift>(request.Candidates);

        foreach (var calendarEvent in await _shiftReader.ListAsync(worker, fromUtc, toUtc, cancellationToken))
        {
            var key = calendarEvent.SourceId ?? calendarEvent.ItemId;

            if (candidateKeys.Contains(key) || request.IgnoredShiftKeys.Contains(key) || !ShiftEvents.TryRead(calendarEvent, out var data))
            {
                continue;
            }

            shifts.Add(new LaborRuleShift
            {
                ShiftKey = key,
                TeamId = data.TeamId,
                Date = DateOnly.FromDateTime(CalendarTime.ToLocal(calendarEvent.StartUtc, timeZoneId)),
                StartUtc = calendarEvent.StartUtc,
                EndUtc = calendarEvent.EndUtc,
                PaidMinutes = ShiftEvents.PaidMinutes(calendarEvent, data, codes),
                BreakMinutes = (int)data.Segments.Where(segment => segment.Kind is ActivityKind.Break or ActivityKind.Meal).Sum(segment => (segment.EndUtc - segment.StartUtc).TotalMinutes),
            });
        }

        var context = new LaborRuleEvaluationContext
        {
            Worker = worker,
            TeamId = request.TeamId,
            TimeZoneId = timeZoneId,
            RuleSet = result.RuleSet,
            NowUtc = _clock.UtcNow,
            Shifts = shifts.OrderBy(shift => shift.StartUtc).ToArray(),
            Changes = request.Changes.ToArray(),
            TimeOff = (await _shiftReader.ListTimeOffAsync(worker, fromUtc, toUtc, cancellationToken))
                .Select(calendarEvent => (calendarEvent.StartUtc, calendarEvent.EndUtc))
                .ToArray(),
        };
        var catalog = _rules.ToDictionary(rule => rule.Key, StringComparer.Ordinal);

        foreach (var instance in result.RuleSet.Rules)
        {
            if (catalog.TryGetValue(instance.RuleKey ?? string.Empty, out var rule) && !rule.RecordsOnly)
            {
                await rule.EvaluateAsync(context, instance, cancellationToken);
            }
        }

        result.Violations = context.Violations;

        return result;
    }
}
