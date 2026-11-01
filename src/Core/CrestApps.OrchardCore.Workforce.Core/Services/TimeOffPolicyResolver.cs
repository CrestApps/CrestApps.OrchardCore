using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Works out which policy of each type applies to one worker on a day: the worker's own assignment first, then the
/// assignment of the nearest team up the tree from the teams they belong to that day.
/// </summary>
internal sealed class TimeOffPolicyResolver
{
    private readonly WorkerProfile _worker;
    private readonly IReadOnlyDictionary<string, WorkforceTeam> _teams;

    public TimeOffPolicyResolver(WorkerProfile worker, IReadOnlyList<TimeOffPolicy> policies, IReadOnlyDictionary<string, WorkforceTeam> teams)
    {
        _worker = worker;
        _teams = teams;
        Policies = policies;
    }

    /// <summary>
    /// Gets every policy assigned to the worker or to one of their teams, at any date.
    /// </summary>
    public IReadOnlyList<TimeOffPolicy> Policies { get; }

    public TimeOffPolicy Resolve(string typeId, DateOnly date)
    {
        var distances = Distances(date);
        TimeOffPolicy best = null;
        var bestDistance = int.MaxValue;

        foreach (var policy in Policies.Where(policy => policy.Enabled && policy.TypeId == typeId).OrderBy(policy => policy.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            foreach (var assignment in policy.Assignments.Where(assignment => assignment.IsEffectiveOn(date)))
            {
                var distance = assignment.WorkerId == _worker.ItemId
                    ? 0
                    : !string.IsNullOrEmpty(assignment.TeamId) && distances.TryGetValue(assignment.TeamId, out var teamDistance) ? teamDistance : int.MaxValue;

                if (distance < bestDistance)
                {
                    best = policy;
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    public IReadOnlyList<TimeOffPolicy> ListOn(DateOnly date)
    {
        return Policies
            .Select(policy => policy.TypeId)
            .Distinct(StringComparer.Ordinal)
            .Select(typeId => Resolve(typeId, date))
            .Where(policy => policy is not null)
            .OrderBy(policy => policy.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public bool AppliesOn(TimeOffPolicy policy, DateOnly date)
    {
        return Resolve(policy.TypeId, date)?.ItemId == policy.ItemId;
    }

    /// <summary>
    /// Returns the first day any of the policy's assignments can apply to the worker: the earliest start, or the hire
    /// date (or the day the policy was made) when an assignment has none.
    /// </summary>
    public DateOnly EligibleFrom(TimeOffPolicy policy)
    {
        var relevant = Relevant(policy).ToArray();
        var fallback = _worker.HireDate
            ?? (policy.CreatedUtc != default ? DateOnly.FromDateTime(policy.CreatedUtc) : DateOnly.FromDateTime(_worker.CreatedUtc));

        if (relevant.Length == 0 || relevant.Any(assignment => !assignment.EffectiveFrom.HasValue))
        {
            return fallback;
        }

        return relevant.Min(assignment => assignment.EffectiveFrom.Value);
    }

    /// <summary>
    /// Returns the last day any of the policy's assignments can apply to the worker, or empty for no end.
    /// </summary>
    public DateOnly? EligibleTo(TimeOffPolicy policy)
    {
        var relevant = Relevant(policy).ToArray();

        if (relevant.Length == 0 || relevant.Any(assignment => !assignment.EffectiveTo.HasValue))
        {
            return null;
        }

        return relevant.Max(assignment => assignment.EffectiveTo.Value);
    }

    private IEnumerable<TimeOffPolicyAssignment> Relevant(TimeOffPolicy policy)
    {
        var teams = new HashSet<string>(StringComparer.Ordinal);

        foreach (var membership in _worker.TeamMemberships)
        {
            foreach (var (teamId, _) in Lineage(membership.TeamId))
            {
                teams.Add(teamId);
            }
        }

        return policy.Assignments.Where(assignment => assignment.WorkerId == _worker.ItemId || (!string.IsNullOrEmpty(assignment.TeamId) && teams.Contains(assignment.TeamId)));
    }

    // How far up the tree each team is from a team the worker belongs to that day: 1 for their own team.
    private Dictionary<string, int> Distances(DateOnly date)
    {
        var distances = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var membership in _worker.MembershipsOn(date))
        {
            foreach (var (teamId, depth) in Lineage(membership.TeamId))
            {
                if (!distances.TryGetValue(teamId, out var known) || depth + 1 < known)
                {
                    distances[teamId] = depth + 1;
                }
            }
        }

        return distances;
    }

    private IEnumerable<(string TeamId, int Depth)> Lineage(string teamId)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var depth = 0;

        while (!string.IsNullOrEmpty(teamId) && seen.Add(teamId))
        {
            yield return (teamId, depth++);

            teamId = _teams.TryGetValue(teamId, out var team) ? team.ParentId : null;
        }
    }
}
