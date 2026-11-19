using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using OrchardCore.ContentManagement;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Finishes what a request left: the open incidents of solved problems, beyond the ones solved with the problem.
/// </summary>
public sealed class TicketProblemCascade
{
    /// <summary>
    /// How many incidents one run solves at most.
    /// </summary>
    public const int PerRun = 200;

    private readonly ISession _session;
    private readonly ITicketService _tickets;
    private readonly TicketProblemObserver _observer;

    public TicketProblemCascade(ISession session, ITicketService tickets, IEnumerable<ITicketObserver> observers)
    {
        _session = session;
        _tickets = tickets;
        _observer = observers.OfType<TicketProblemObserver>().FirstOrDefault();
    }

    /// <summary>
    /// Solves the open incidents of solved problems, up to <see cref="PerRun"/>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many were solved.</returns>
    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        if (_observer is null)
        {
            return 0;
        }

        var problemIds = (await _session.QueryIndex<TicketLinkIndex>(index => index.Kind == TicketLinkKinds.Problem).ListAsync(cancellationToken))
            .Select(index => index.LinkedTicketId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (problemIds.Length == 0)
        {
            return 0;
        }

        var solvedProblems = await _session.QueryIndex<TicketIndex>(index => index.ContentItemId.IsIn(problemIds) && index.StatusCategory == TicketStatusCategory.Solved).ListAsync(cancellationToken);
        var solved = 0;

        foreach (var problem in solvedProblems)
        {
            if (solved >= PerRun)
            {
                break;
            }

            if (await _tickets.GetAsync(problem.ContentItemId) is not { } item)
            {
                continue;
            }

            solved += await _observer.SolveIncidentsAsync(item, item.GetOrCreate<TicketPart>().StatusId, by: null, PerRun - solved, cancellationToken);
        }

        return solved;
    }
}
