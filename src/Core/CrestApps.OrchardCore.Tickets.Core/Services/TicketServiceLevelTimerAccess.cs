using System.Security.Claims;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Lets a user see the service-level timers of the tickets in their scope.
/// </summary>
public sealed class TicketServiceLevelTimerAccess : IServiceLevelTimerAccessProvider
{
    private readonly ITicketAccessService _access;
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketServiceLevelTimerAccess"/> class.
    /// </summary>
    public TicketServiceLevelTimerAccess(ITicketAccessService access, ISession session)
    {
        _access = access;
        _session = session;
    }

    /// <inheritdoc/>
    public string SubjectKind => TicketsConstants.RecordKind;

    /// <inheritdoc/>
    public async Task<IReadOnlySet<string>> GetVisibleAsync(ClaimsPrincipal user, IReadOnlyCollection<string> subjectIds, CancellationToken cancellationToken = default)
    {
        if (subjectIds is not { Count: > 0 } || await _access.ApplyScopeAsync(user, new TicketQuery(), cancellationToken) is not { } scope)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        var ids = subjectIds.ToArray();
        var visible = await _session.QueryIndex<TicketIndex>(TicketScopeFilter.ToPredicate(scope))
            .Where(index => index.ContentItemId.IsIn(ids))
            .ListAsync(cancellationToken);

        return visible.Select(index => index.ContentItemId).ToHashSet(StringComparer.Ordinal);
    }
}
