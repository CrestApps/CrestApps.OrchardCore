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
/// the same status, through the ticket service one by one (its own lock, its events, the requester's "solved" e-mail),
/// whatever desk it is on. The agent who solved the problem is recorded, with the problem as the reason. A request
/// solves the first <see cref="SolvedInline"/>; the <c>Solve incidents of solved problems</c> task does the rest.
/// </summary>
public sealed class TicketProblemObserver : ITicketObserver
{
    /// <summary>
    /// How many incidents are solved in the request that solved their problem.
    /// </summary>
    public const int SolvedInline = 25;

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

        await SolveIncidentsAsync(ticket, current.ItemId, TicketEventActor.Current, SolvedInline, cancellationToken);
    }

    /// <summary>
    /// Solves up to <paramref name="max"/> of a solved problem's open incidents.
    /// </summary>
    /// <param name="problem">The problem.</param>
    /// <param name="statusId">The status to give them.</param>
    /// <param name="by">Who solved the problem, if known.</param>
    /// <param name="max">How many at most.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many were solved.</returns>
    public async Task<int> SolveIncidentsAsync(ContentItem problem, string statusId, TicketActor by, int max, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(problem);

        var open = await OpenIncidentsAsync(_services.GetRequiredService<ISession>(), problem.ContentItemId, max, cancellationToken);

        if (open.Count == 0)
        {
            return 0;
        }

        var tickets = _services.GetRequiredService<ITicketService>();
        var number = problem.GetOrCreate<TicketPart>().Number;
        var actor = by is { } agent && !agent.FromWorkflow && !agent.Party.IsEmpty
            ? new TicketActor { Party = agent.Party, Name = agent.Name }
            : new TicketActor { Name = S["Problem {0}", number].Value, FromWorkflow = by?.FromWorkflow == true };
        var reason = S["Solved with its problem {0}.", number].Value;
        var solved = 0;

        foreach (var incidentId in open)
        {
            var result = await tickets.UpdateAsync(incidentId, new TicketUpdate { StatusId = statusId, Reason = reason }, actor, cancellationToken);

            if (result.Succeeded)
            {
                solved++;
            }
            else
            {
                _logger.LogWarning("The incident {IncidentId} of the solved problem {ProblemId} wasn't solved: {Errors}", incidentId, problem.ContentItemId, string.Join(" ", result.Errors));
            }
        }

        return solved;
    }

    /// <summary>
    /// The open incidents of a problem.
    /// </summary>
    /// <param name="session">The session.</param>
    /// <param name="problemId">The problem.</param>
    /// <param name="max">How many at most.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Their ids.</returns>
    public static async Task<IReadOnlyList<string>> OpenIncidentsAsync(ISession session, string problemId, int max, CancellationToken cancellationToken = default)
    {
        var incidentIds = (await session.QueryIndex<TicketLinkIndex>(index => index.LinkedTicketId == problemId && index.Kind == TicketLinkKinds.Problem).ListAsync(cancellationToken))
            .Select(index => index.TicketId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (incidentIds.Length == 0)
        {
            return [];
        }

        return [.. (await session.QueryIndex<TicketIndex>(index => index.ContentItemId.IsIn(incidentIds) && index.StatusCategory.IsNotIn(_finished) && index.MergedIntoId == null).Take(max).ListAsync(cancellationToken))
            .Select(index => index.ContentItemId)];
    }
}
