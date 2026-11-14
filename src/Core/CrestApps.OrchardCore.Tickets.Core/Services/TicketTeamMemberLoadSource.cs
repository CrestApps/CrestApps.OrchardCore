using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// How much ticket work each team member has, for team assignment: the tickets assigned to them still being worked on
/// (new, open, pending, on hold), and when they were last given one, so round robin takes turns.
/// </summary>
public sealed class TicketTeamMemberLoadSource : ITeamMemberLoadSource
{
    private static readonly TicketStatusCategory[] _open = [TicketStatusCategory.New, TicketStatusCategory.Open, TicketStatusCategory.Pending, TicketStatusCategory.OnHold];

    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketTeamMemberLoadSource"/> class.
    /// </summary>
    /// <param name="session">The session.</param>
    public TicketTeamMemberLoadSource(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc/>
    public string WorkKind => TicketsConstants.RecordKind;

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, TeamMemberLoad>> GetLoadAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
    {
        var ids = userIds?.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).ToArray() ?? [];
        var loads = ids.ToDictionary(id => id, _ => new TeamMemberLoad(), StringComparer.Ordinal);

        if (ids.Length == 0)
        {
            return loads;
        }

        var open = await _session.QueryIndex<TicketIndex>(index => index.AssigneeId.IsIn(ids) && index.StatusCategory.IsIn(_open))
            .ListAsync(cancellationToken);

        foreach (var row in open)
        {
            loads[row.AssigneeId].Open++;
        }

        // One seek per member on (AssigneeId, AssignedUtc): a team is small, and this reads no more than one row each.
        foreach (var id in ids)
        {
            var latest = await _session.QueryIndex<TicketIndex>(index => index.AssigneeId == id && index.AssignedUtc != null)
                .OrderByDescending(index => index.AssignedUtc)
                .FirstOrDefaultAsync(cancellationToken);

            loads[id].LastAssignedUtc = latest?.AssignedUtc;
        }

        return loads;
    }
}
