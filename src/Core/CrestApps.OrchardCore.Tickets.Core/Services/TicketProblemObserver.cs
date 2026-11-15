using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Solves a problem's incidents with it: when a ticket is solved, every open ticket linked to it as its problem moves to
/// the same status, which tells their requesters as any solve does.
/// </summary>
public sealed class TicketProblemObserver : ITicketObserver
{
    private static readonly TicketStatusCategory[] _finished = [TicketStatusCategory.Solved, TicketStatusCategory.Closed];

    private readonly IServiceProvider _services;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public TicketProblemObserver(IServiceProvider services, ILogger<TicketProblemObserver> logger, IStringLocalizer<TicketProblemObserver> stringLocalizer)
    {
        _services = services;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task StatusChangedAsync(ContentItem ticket, TicketStatus previous, TicketStatus current, CancellationToken cancellationToken = default)
    {
        if (current?.Category != TicketStatusCategory.Solved || previous?.Category == TicketStatusCategory.Solved)
        {
            return;
        }

        var problemId = ticket.ContentItemId;
        var session = _services.GetRequiredService<ISession>();
        var incidentIds = (await session.QueryIndex<TicketLinkIndex>(index => index.LinkedTicketId == problemId && index.Kind == TicketLinkKinds.Problem).ListAsync(cancellationToken))
            .Select(index => index.TicketId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (incidentIds.Length == 0)
        {
            return;
        }

        var open = (await session.QueryIndex<TicketIndex>(index => index.ContentItemId.IsIn(incidentIds) && index.StatusCategory.IsNotIn(_finished) && index.MergedIntoId == null).ListAsync(cancellationToken))
            .Select(index => index.ContentItemId)
            .ToList();

        var tickets = _services.GetRequiredService<ITicketService>();
        var actor = new TicketActor { Name = S["Problem {0}", ticket.GetOrCreate<TicketPart>().Number].Value };

        foreach (var incidentId in open)
        {
            var result = await tickets.UpdateAsync(incidentId, new TicketUpdate { StatusId = current.ItemId }, actor, cancellationToken);

            if (!result.Succeeded)
            {
                _logger.LogWarning("The incident {IncidentId} of the solved problem {ProblemId} wasn't solved: {Errors}", incidentId, problemId, string.Join(" ", result.Errors));
            }
        }
    }
}
