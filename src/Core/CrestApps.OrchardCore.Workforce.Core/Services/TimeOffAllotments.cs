using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Finds the allotments a request falls under and the dates on which they are full. An approved or pending request
/// holds a place on each day it counts something; a waitlisted one doesn't.
/// </summary>
internal sealed class TimeOffAllotments
{
    private static readonly TimeOffRequestStatus[] _holding = [TimeOffRequestStatus.Approved, TimeOffRequestStatus.Pending];

    private readonly TimeOffAllotmentStore _allotmentStore;
    private readonly TimeOffRequestStore _requestStore;
    private readonly TimeOffTypeStore _typeStore;
    private readonly TeamStore _teamStore;

    public TimeOffAllotments(TimeOffAllotmentStore allotmentStore, TimeOffRequestStore requestStore, TimeOffTypeStore typeStore, TeamStore teamStore)
    {
        _allotmentStore = allotmentStore;
        _requestStore = requestStore;
        _typeStore = typeStore;
        _teamStore = teamStore;
    }

    /// <summary>
    /// Lists the enabled allotments of the team and the teams above it that count the type.
    /// </summary>
    public async Task<IReadOnlyList<TimeOffAllotment>> ListForAsync(string teamId, TimeOffType type, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(teamId) || type is null)
        {
            return [];
        }

        var lineage = Lineage(await _teamStore.ListAsync(cancellationToken), teamId);

        return (await _allotmentStore.ListAsync(cancellationToken))
            .Where(allotment => allotment.Enabled && allotment.Counts(type) && lineage.Contains(allotment.TeamId))
            .ToArray();
    }

    /// <summary>
    /// Returns, for each day that counts something, the allotments that have no place left that day.
    /// </summary>
    public async Task<IReadOnlyList<(DateOnly Date, TimeOffAllotment Allotment)>> FullAsync(IReadOnlyList<TimeOffAllotment> allotments, IReadOnlyList<TimeOffRequestDay> days, string excludeRequestId, CancellationToken cancellationToken = default)
    {
        var counted = days.Where(day => day.Amount > 0).ToArray();

        if (allotments.Count == 0 || counted.Length == 0)
        {
            return [];
        }

        var teams = await _teamStore.ListAsync(cancellationToken);
        var types = (await _typeStore.ListAsync(cancellationToken)).ToDictionary(type => type.ItemId, StringComparer.Ordinal);
        var from = counted.Min(day => day.Date);
        var to = counted.Max(day => day.Date);
        var full = new List<(DateOnly, TimeOffAllotment)>();

        foreach (var allotment in allotments)
        {
            var subtree = await TeamTree.GetSubtreeIdsAsync(teams, allotment.TeamId);
            var holding = (await _requestStore.ListForTeamsAsync(subtree.ToArray(), _holding, from, to, cancellationToken))
                .Where(request => request.ItemId != excludeRequestId && types.TryGetValue(request.TypeId ?? string.Empty, out var type) && allotment.Counts(type))
                .ToArray();

            foreach (var day in counted)
            {
                var used = holding.Count(request => request.Days.Any(item => item.Date == day.Date && item.Amount > 0));

                if (used >= allotment.SlotsOn(day.Date))
                {
                    full.Add((day.Date, allotment));
                }
            }
        }

        return full;
    }

    /// <summary>
    /// Returns what a request should become: approved when it fits allotments that all approve on their own, waitlisted
    /// when one that waitlists is full, and pending otherwise.
    /// </summary>
    public static (TimeOffRequestStatus Status, bool AutoApproved) Decide(IReadOnlyList<TimeOffAllotment> allotments, IReadOnlyList<(DateOnly Date, TimeOffAllotment Allotment)> full)
    {
        if (allotments.Count == 0)
        {
            return (TimeOffRequestStatus.Pending, false);
        }

        if (full.Count > 0)
        {
            return full.Any(item => item.Allotment.WhenFull == TimeOffAllotmentFullAction.Waitlist)
                ? (TimeOffRequestStatus.Waitlisted, false)
                : (TimeOffRequestStatus.Pending, false);
        }

        return allotments.All(allotment => allotment.AutoApprove)
            ? (TimeOffRequestStatus.Approved, true)
            : (TimeOffRequestStatus.Pending, false);
    }

    public static HashSet<string> Lineage(IEnumerable<WorkforceTeam> teams, string teamId)
    {
        var parents = teams.ToDictionary(team => team.ItemId, team => team.ParentId, StringComparer.Ordinal);
        var lineage = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrEmpty(teamId) && lineage.Add(teamId))
        {
            teamId = parents.TryGetValue(teamId, out var parentId) ? parentId : null;
        }

        return lineage;
    }
}
