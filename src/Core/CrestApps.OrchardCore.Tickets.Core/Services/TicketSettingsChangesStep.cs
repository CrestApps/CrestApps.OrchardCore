using CrestApps.Core.Services;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.YesSql.Core.Upgrades;
using OrchardCore.ContentManagement;
using YesSql;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Carries a status's category and "pauses timers", or a priority's rank, to the tickets that have it, after the record
/// is saved: tickets keep these copies for the list, the views and the reports, and a status moved from On hold to
/// Pending must list and pause its tickets as Pending. It works through the tickets a batch at a time and picks up where
/// it stopped on its next run.
/// </summary>
public sealed class TicketSettingsChangesStep : IResumableStep
{
    /// <summary>
    /// The step's name.
    /// </summary>
    public const string StepName = "Tickets.SettingsChanges";

    /// <summary>
    /// The key prefix of a changed status.
    /// </summary>
    public const string StatusPrefix = "status:";

    /// <summary>
    /// The key prefix of a changed priority.
    /// </summary>
    public const string PriorityPrefix = "priority:";

    private const int TicketsPerRun = 200;

    private readonly ISession _session;
    private readonly IContentManager _contentManager;
    private readonly ICatalog<TicketStatus> _statuses;
    private readonly ICatalog<TicketPriority> _priorities;
    private readonly IServiceLevelTracker _serviceLevels;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketSettingsChangesStep"/> class.
    /// </summary>
    public TicketSettingsChangesStep(ISession session, IContentManager contentManager, ICatalog<TicketStatus> statuses, ICatalog<TicketPriority> priorities, IServiceLevelTracker serviceLevels)
    {
        _session = session;
        _contentManager = contentManager;
        _statuses = statuses;
        _priorities = priorities;
        _serviceLevels = serviceLevels;
    }

    /// <inheritdoc/>
    public string Name => StepName;

    /// <inheritdoc/>
    public async Task<bool> RunAsync(ResumableStepContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var budget = TicketsPerRun;

        foreach (var key in context.Data.Keys.Order(StringComparer.Ordinal).ToList())
        {
            if (budget <= 0)
            {
                break;
            }

            var lastId = long.TryParse(context.Data[key], out var cursor) ? cursor : 0;
            var (done, last, used) = key.StartsWith(StatusPrefix, StringComparison.Ordinal)
                ? await CarryStatusAsync(key[StatusPrefix.Length..], lastId, budget, cancellationToken)
                : await CarryPriorityAsync(key[PriorityPrefix.Length..], lastId, budget, cancellationToken);

            budget -= used;

            if (done)
            {
                context.Data.Remove(key);
            }
            else
            {
                context.Data[key] = last.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return context.Data.Count == 0;
    }

    private async Task<(bool Done, long Last, int Used)> CarryStatusAsync(string statusId, long afterId, int budget, CancellationToken cancellationToken)
    {
        if (await _statuses.FindByIdAsync(statusId, cancellationToken) is not { } status)
        {
            return (true, afterId, 0);
        }

        var rows = (await _session.QueryIndex<TicketIndex>(index => index.StatusId == statusId && index.Id > afterId)
            .OrderBy(index => index.Id)
            .Take(budget)
            .ListAsync(cancellationToken)).ToList();

        foreach (var row in rows)
        {
            if (await _contentManager.GetAsync(row.ContentItemId, VersionOptions.Latest) is not { } ticket || !ticket.TryGet<TicketPart>(out var part))
            {
                continue;
            }

            if (part.StatusCategory != status.Category)
            {
                part.StatusCategory = status.Category;
                ticket.Apply(part);
                await _contentManager.UpdateAsync(ticket);
            }

            // The timers of a ticket still being worked on follow whether its status pauses them; both calls leave a
            // timer already in that state alone.
            if (status.Category is not TicketStatusCategory.Solved and not TicketStatusCategory.Closed)
            {
                var subject = DefaultTicketService.SubjectOf(ticket.ContentItemId);

                if (status.PausesTimers)
                {
                    await _serviceLevels.PauseAsync(subject, cancellationToken: cancellationToken);
                }
                else
                {
                    await _serviceLevels.ResumeAsync(subject, cancellationToken: cancellationToken);
                }
            }
        }

        return (rows.Count < budget, rows.Count > 0 ? rows[^1].Id : afterId, rows.Count);
    }

    private async Task<(bool Done, long Last, int Used)> CarryPriorityAsync(string priorityId, long afterId, int budget, CancellationToken cancellationToken)
    {
        if (await _priorities.FindByIdAsync(priorityId, cancellationToken) is not { } priority)
        {
            return (true, afterId, 0);
        }

        var rank = priority.Rank;
        var rows = (await _session.QueryIndex<TicketIndex>(index => index.PriorityId == priorityId && index.PriorityRank != rank && index.Id > afterId)
            .OrderBy(index => index.Id)
            .Take(budget)
            .ListAsync(cancellationToken)).ToList();

        foreach (var row in rows)
        {
            if (await _contentManager.GetAsync(row.ContentItemId, VersionOptions.Latest) is { } ticket && ticket.TryGet<TicketPart>(out var part))
            {
                part.PriorityRank = rank;
                ticket.Apply(part);
                await _contentManager.UpdateAsync(ticket);
            }
        }

        return (rows.Count < budget, rows.Count > 0 ? rows[^1].Id : afterId, rows.Count);
    }
}
