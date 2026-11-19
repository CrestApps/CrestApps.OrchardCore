using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Services;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.BackgroundTasks;
using OrchardCore.ContentManagement;
using OrchardCore.Data.Documents;
using OrchardCore.Documents;
using OrchardCore.Modules;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Tickets.Workflows;

/// <summary>
/// The ticket actions of published workflows that start with <see cref="TicketActionRequestedEvent"/>.
/// </summary>
internal sealed class WorkflowTicketActionSource : ITicketActionSource
{
    private readonly IWorkflowTypeStore _workflowTypes;
    private readonly IWorkflowManager _workflowManager;

    public WorkflowTicketActionSource(IWorkflowTypeStore workflowTypes, IWorkflowManager workflowManager)
    {
        _workflowTypes = workflowTypes;
        _workflowManager = workflowManager;
    }

    public async Task<IReadOnlyList<string>> ListAsync(ContentItem ticket, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        ticket.TryGet<TicketPart>(out var part);
        var names = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);

        foreach (var workflowType in await _workflowTypes.GetByStartActivityAsync(TicketsConstants.Events.ActionRequested))
        {
            if (!workflowType.IsEnabled)
            {
                continue;
            }

            // Orchard Core starts a workflow type at its first start activity of an event, so only that one is offered.
            if (workflowType.Activities.FirstOrDefault(activity => activity.IsStart && activity.Name == TicketsConstants.Events.ActionRequested) is { } activity
                && activity.Properties?[nameof(TicketActionRequestedEvent.ActionName)]?.ToString()?.Trim() is { Length: > 0 } name
                && TicketActivityProperties.OfferedOnDesk(activity, part?.DeskId))
            {
                names.Add(name);
            }
        }

        return [.. names];
    }

    public async Task<bool> RequestAsync(ContentItem ticket, string actionName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actionName) || !(await ListAsync(ticket, cancellationToken)).Contains(actionName.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var input = TicketWorkflowInput.Of(ticket);
        input[TicketWorkflowInput.ActionName] = actionName.Trim();

        await _workflowManager.TriggerEventAsync(TicketsConstants.Events.ActionRequested, input, ticket.ContentItemId);

        return true;
    }
}

/// <summary>
/// Reads the settings of a ticket workflow activity from a stored workflow type.
/// </summary>
internal static class TicketActivityProperties
{
    public static bool OfferedOnDesk(ActivityRecord activity, string deskId)
        => activity.Properties?[nameof(TicketEventActivity.DeskIds)] is not JsonArray desks || desks.Count == 0
            || (!string.IsNullOrEmpty(deskId) && desks.Any(desk => string.Equals(desk?.ToString(), deskId, StringComparison.Ordinal)));

    public static IReadOnlyList<string> Strings(ActivityRecord activity, string name)
        => activity.Properties?[name] is JsonArray values ? [.. values.Select(value => value?.ToString()).Where(value => !string.IsNullOrEmpty(value))] : [];

    public static double Number(ActivityRecord activity, string name, double fallback)
        => activity.Properties?[name] is JsonValue value && value.TryGetValue<double>(out var number) ? number : fallback;

    public static bool Flag(ActivityRecord activity, string name)
        => activity.Properties?[name] is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

    public static string Text(ActivityRecord activity, string name, string fallback)
        => activity.Properties?[name]?.ToString() is { Length: > 0 } text ? text : fallback;
}

/// <summary>
/// Where each time-elapsed start's scan stopped, so the next run goes on from there and every ticket is reached in turn.
/// </summary>
public sealed class TicketTimeElapsedDocument : global::OrchardCore.Data.Documents.Document
{
    /// <summary>
    /// Gets or sets the markers of an earlier version, which kept every start in this one document. They are dropped on
    /// the next scan; the markers now live in <see cref="Core.Indexes.TicketTimeElapsedMark"/>.
    /// </summary>
    public Dictionary<string, DateTime> Started { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the ticket index row each start's scan stopped after, by <c>workflow type:activity</c>.
    /// </summary>
    public Dictionary<string, long> Cursors { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Starts <see cref="TicketTimeElapsedEvent"/> for the tickets whose time has passed. For each start it reads only the
/// tickets whose moment is at or before now minus the start's hours, a batch at a time from where the last run stopped,
/// and leaves out the ones it already fired for at that moment.
/// </summary>
internal sealed class TicketTimeElapsedScanner
{
    // The most tickets one start looks at in a run; the next run goes on from there.
    private const int TicketsPerStart = 500;

    // The most markers of tickets no longer being worked on removed in a run.
    private const int MarksCleanedPerRun = 500;

    private static readonly TicketStatusCategory[] _open = [TicketStatusCategory.New, TicketStatusCategory.Open, TicketStatusCategory.Pending, TicketStatusCategory.OnHold];

    private readonly IWorkflowTypeStore _workflowTypes;
    private readonly IWorkflowManager _workflowManager;
    private readonly ITicketService _tickets;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly IDocumentManager<TicketTimeElapsedDocument> _documents;
    private readonly ISession _session;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;

    public TicketTimeElapsedScanner(
        IWorkflowTypeStore workflowTypes,
        IWorkflowManager workflowManager,
        ITicketService tickets,
        ICatalog<TicketDesk> desks,
        IDocumentManager<TicketTimeElapsedDocument> documents,
        ISession session,
        IServiceProvider serviceProvider,
        IClock clock)
    {
        _workflowTypes = workflowTypes;
        _workflowManager = workflowManager;
        _tickets = tickets;
        _desks = desks;
        _documents = documents;
        _session = session;
        _serviceProvider = serviceProvider;
        _clock = clock;
    }

    public async Task<int> ScanAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var document = await _documents.GetOrCreateMutableAsync();
        document.Started.Clear();

        var workflowTypes = (await _workflowTypes.GetByStartActivityAsync(TicketsConstants.Events.TimeElapsed)).Where(type => type.IsEnabled).ToList();
        var desks = (await _desks.GetAllAsync(cancellationToken)).ToDictionary(desk => desk.ItemId, StringComparer.Ordinal);
        var openHours = _serviceProvider.GetService<IOpenHoursService>();
        var started = 0;
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var workflowType in workflowTypes)
        {
            // Orchard Core starts a workflow type at its first start activity of an event, so only that one is scanned.
            if (workflowType.Activities.FirstOrDefault(activity => activity.IsStart && activity.Name == TicketsConstants.Events.TimeElapsed) is not { } activity)
            {
                continue;
            }

            var key = $"{workflowType.WorkflowTypeId}:{activity.ActivityId}";
            keys.Add(key);

            var hours = TicketActivityProperties.Number(activity, nameof(TicketTimeElapsedEvent.Hours), 24);
            var since = TicketActivityProperties.Text(activity, nameof(TicketTimeElapsedEvent.Since), TicketTimeElapsedSince.Created);
            var businessHours = TicketActivityProperties.Flag(activity, nameof(TicketTimeElapsedEvent.BusinessHours));
            var categories = TicketActivityProperties.Strings(activity, nameof(TicketTimeElapsedEvent.Categories))
                .Select(name => Enum.TryParse<TicketStatusCategory>(name, ignoreCase: true, out var category) ? category : (TicketStatusCategory?)null)
                .OfType<TicketStatusCategory>()
                .ToArray();
            var deskIds = TicketActivityProperties.Strings(activity, nameof(TicketEventActivity.DeskIds)).ToArray();

            // Business time is never more than calendar time, so a ticket whose moment is later than this can't be due.
            var cutoff = now.AddHours(-hours);
            var afterId = document.Cursors.GetValueOrDefault(key);
            var rows = await CandidatesAsync(since, cutoff, categories.Length == 0 ? _open : categories, deskIds, afterId, cancellationToken);

            document.Cursors[key] = rows.Count < TicketsPerStart ? 0 : rows[^1].Id;

            if (rows.Count == 0)
            {
                continue;
            }

            var ticketIds = rows.Select(row => row.ContentItemId).ToArray();
            var marks = (await _session.Query<TicketTimeElapsedMark, TicketTimeElapsedMarkIndex>(mark => mark.StartKey == key && mark.TicketId.IsIn(ticketIds), TicketsConstants.CollectionName).ListAsync(cancellationToken))
                .GroupBy(mark => mark.TicketId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(mark => mark.Moment).First(), StringComparer.Ordinal);

            foreach (var row in rows)
            {
                if (MomentOf(since, row) is not { } moment)
                {
                    continue;
                }

                marks.TryGetValue(row.ContentItemId, out var mark);

                if (mark is not null && mark.Moment >= moment)
                {
                    continue;
                }

                var elapsed = businessHours && openHours is not null && desks.TryGetValue(row.DeskId ?? string.Empty, out var desk) && !string.IsNullOrEmpty(desk.HoursScheduleId)
                    ? await openHours.GetOpenTimeBetweenAsync(desk.HoursScheduleId, moment, now, cancellationToken: cancellationToken)
                    : now - moment;

                if (elapsed.TotalHours < hours || await _tickets.GetAsync(row.ContentItemId) is not { } ticket)
                {
                    continue;
                }

                mark ??= new TicketTimeElapsedMark { StartKey = key, TicketId = row.ContentItemId };
                mark.Moment = moment;
                mark.FiredUtc = now;
                await _session.SaveAsync(mark, false, TicketsConstants.CollectionName, cancellationToken);

                var input = TicketWorkflowInput.Of(ticket);
                input[TicketWorkflowInput.ActivityId] = activity.ActivityId;
                input[TicketWorkflowInput.WorkflowTypeId] = workflowType.WorkflowTypeId;

                await _workflowManager.TriggerEventAsync(TicketsConstants.Events.TimeElapsed, input, ticket.ContentItemId);
                started++;
            }
        }

        foreach (var key in document.Cursors.Keys.Where(key => !keys.Contains(key)).ToList())
        {
            document.Cursors.Remove(key);
        }

        await _documents.UpdateAsync(document);
        await CleanMarksAsync(cancellationToken);

        return started;
    }

    // The open tickets of a start whose moment is at or before the cutoff, after the cursor, in index order.
    private async Task<List<TicketIndex>> CandidatesAsync(string since, DateTime cutoff, TicketStatusCategory[] categories, string[] deskIds, long afterId, CancellationToken cancellationToken)
    {
        var query = since switch
        {
            TicketTimeElapsedSince.LastCustomerReply => _session.QueryIndex<TicketIndex>(index => index.LastCustomerActivityUtc != null && index.LastCustomerActivityUtc <= cutoff),
            TicketTimeElapsedSince.LastAgentReply => _session.QueryIndex<TicketIndex>(index => index.LastAgentActivityUtc != null && index.LastAgentActivityUtc <= cutoff),
            TicketTimeElapsedSince.StatusChanged => _session.QueryIndex<TicketIndex>(index => index.StatusChangedUtc != null && index.StatusChangedUtc <= cutoff),
            _ => _session.QueryIndex<TicketIndex>(index => index.CreatedUtc <= cutoff),
        };

        query = query.Where(index => index.StatusCategory.IsIn(categories) && index.MergedIntoId == null && index.Id > afterId);

        if (deskIds.Length > 0)
        {
            query = query.Where(index => index.DeskId.IsIn(deskIds));
        }

        return [.. await query.OrderBy(index => index.Id).Take(TicketsPerStart).ListAsync(cancellationToken)];
    }

    private static DateTime? MomentOf(string since, TicketIndex row)
        => since switch
        {
            TicketTimeElapsedSince.LastCustomerReply => row.LastCustomerActivityUtc,
            TicketTimeElapsedSince.LastAgentReply => row.LastAgentActivityUtc,
            TicketTimeElapsedSince.StatusChanged => row.StatusChangedUtc,
            _ => row.CreatedUtc,
        };

    // A marker is kept while its ticket is being worked on, however long ago it fired, so a start never fires twice for
    // a moment; it goes once the ticket is solved, closed, merged or deleted.
    private async Task CleanMarksAsync(CancellationToken cancellationToken)
    {
        var marks = (await _session.Query<TicketTimeElapsedMark, TicketTimeElapsedMarkIndex>(TicketsConstants.CollectionName)
            .OrderBy(mark => mark.FiredUtc)
            .Take(MarksCleanedPerRun)
            .ListAsync(cancellationToken)).ToList();

        if (marks.Count == 0)
        {
            return;
        }

        var ticketIds = marks.Select(mark => mark.TicketId).Distinct(StringComparer.Ordinal).ToArray();
        var open = (await _session.QueryIndex<TicketIndex>(index => index.ContentItemId.IsIn(ticketIds) && index.StatusCategory.IsIn(_open) && index.MergedIntoId == null).ListAsync(cancellationToken))
            .Select(index => index.ContentItemId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var mark in marks.Where(mark => !open.Contains(mark.TicketId)))
        {
            _session.Delete(mark, TicketsConstants.CollectionName);
        }
    }
}

/// <summary>
/// Starts the ticket time-elapsed workflows every 15 minutes.
/// </summary>
[BackgroundTask(
    Title = "Ticket time elapsed",
    Schedule = "*/15 * * * *",
    Description = "Starts the workflows that wait for a time to pass in a ticket's life.",
    LockTimeout = 1_000,
    LockExpiration = 600_000)]
public sealed class TicketTimeElapsedBackgroundTask : IBackgroundTask
{
    /// <inheritdoc/>
    public Task DoWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
        => this.RunAsync(async run => run.AddItems(await serviceProvider.GetRequiredService<TicketTimeElapsedScanner>().ScanAsync(cancellationToken), "started"));
}
