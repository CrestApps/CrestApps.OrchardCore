using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Closes solved tickets once their desk's auto-close delay has passed since they were solved. A ticket reopened in the
/// meantime is no longer solved, so it isn't closed; a desk with a delay of 0 keeps its solved tickets.
/// </summary>
public sealed class TicketAutoCloser
{
    private readonly ITicketService _tickets;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly ICatalog<TicketStatus> _statuses;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketAutoCloser"/> class.
    /// </summary>
    /// <param name="tickets">The ticket service.</param>
    /// <param name="desks">The desks.</param>
    /// <param name="statuses">The statuses.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TicketAutoCloser(
        ITicketService tickets,
        ICatalog<TicketDesk> desks,
        ICatalog<TicketStatus> statuses,
        IClock clock,
        ILogger<TicketAutoCloser> logger,
        IStringLocalizer<TicketAutoCloser> stringLocalizer)
    {
        _tickets = tickets;
        _desks = desks;
        _statuses = statuses;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Closes the tickets whose delay has passed, at most a number of them per desk in one run.
    /// </summary>
    /// <param name="maxPerDesk">The most tickets of one desk closed in one run; the rest wait for the next run.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many tickets were closed.</returns>
    public async Task<int> CloseDueAsync(int maxPerDesk = 100, CancellationToken cancellationToken = default)
    {
        var closedStatus = TicketLifecycle.FirstOf(await _statuses.GetAllAsync(cancellationToken), TicketStatusCategory.Closed);

        if (closedStatus is null)
        {
            return 0;
        }

        var now = _clock.UtcNow;
        var closed = 0;
        var actor = new TicketActor { Name = S["Auto close"] };

        foreach (var desk in await _desks.GetAllAsync(cancellationToken))
        {
            if (desk.AutoCloseDays <= 0)
            {
                continue;
            }

            // A ticket that can't be closed (a failed result or an exception) is logged and skipped: the next page starts
            // after the tickets still solved, so one bad ticket never holds up the rest of its desk, now or in later runs.
            var cutoff = now.AddDays(-desk.AutoCloseDays);
            var skip = 0;
            var budget = maxPerDesk;

            while (budget > 0)
            {
                var requested = budget;
                var (due, _) = await _tickets.ListAsync(new TicketScopedQuery
                {
                    Query = new TicketQuery
                    {
                        DeskIds = [desk.ItemId],
                        Categories = [TicketStatusCategory.Solved],
                        SolvedBeforeUtc = cutoff,
                        Sort = TicketSort.Created,
                        Skip = skip,
                        Take = requested,
                    },
                    AllDeskIds = [desk.ItemId],
                }, cancellationToken);

                if (due.Count == 0)
                {
                    break;
                }

                foreach (var ticket in due)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    budget--;

                    if (!TicketLifecycle.ShouldAutoClose(ticket.GetOrCreate<TicketPart>(), desk, now))
                    {
                        skip++;

                        continue;
                    }

                    try
                    {
                        var result = await _tickets.UpdateAsync(ticket.ContentItemId, new TicketUpdate { StatusId = closedStatus.ItemId }, actor, cancellationToken);

                        if (result.Succeeded)
                        {
                            closed++;
                        }
                        else
                        {
                            skip++;
                            _logger.LogWarning("The solved ticket {TicketId} couldn't be closed on its own: {Errors}", ticket.ContentItemId, string.Join(" ", result.Errors));
                        }
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        skip++;
                        _logger.LogError(exception, "The solved ticket {TicketId} couldn't be closed on its own and was skipped.", ticket.ContentItemId);
                    }
                }

                // A short page was the last one.
                if (due.Count < requested)
                {
                    break;
                }
            }
        }

        return closed;
    }
}
