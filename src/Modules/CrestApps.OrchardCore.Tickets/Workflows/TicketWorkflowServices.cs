using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Services;
using CrestApps.OrchardCore.Diagnostics;
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

            foreach (var activity in workflowType.Activities.Where(activity => activity.IsStart && activity.Name == TicketsConstants.Events.ActionRequested))
            {
                var name = activity.Properties?[nameof(TicketActionRequestedEvent.ActionName)]?.ToString()?.Trim();

                if (!string.IsNullOrEmpty(name) && TicketActivityProperties.OfferedOnDesk(activity, part?.DeskId))
                {
                    names.Add(name);
                }
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
/// The ticket moments a time-elapsed workflow has already started for, so each starts once per ticket and moment.
/// </summary>
public sealed class TicketTimeElapsedDocument : Document
{
    /// <summary>
    /// Gets or sets the moment each start was for, by <c>workflow type:activity:ticket</c>.
    /// </summary>
    public Dictionary<string, DateTime> Started { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// Starts <see cref="TicketTimeElapsedEvent"/> for the tickets whose time has passed.
/// </summary>
internal sealed class TicketTimeElapsedScanner
{
    // The most tickets one activity looks at in a run; the rest wait for the next run.
    private const int TicketsPerActivity = 500;

    // How long a start is remembered after its moment, so the record doesn't grow without end.
    private static readonly TimeSpan _remembered = TimeSpan.FromDays(180);

    private static readonly TicketStatusCategory[] _open = [TicketStatusCategory.New, TicketStatusCategory.Open, TicketStatusCategory.Pending, TicketStatusCategory.OnHold];

    private readonly IWorkflowTypeStore _workflowTypes;
    private readonly IWorkflowManager _workflowManager;
    private readonly ITicketService _tickets;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly IDocumentManager<TicketTimeElapsedDocument> _documents;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;

    public TicketTimeElapsedScanner(
        IWorkflowTypeStore workflowTypes,
        IWorkflowManager workflowManager,
        ITicketService tickets,
        ICatalog<TicketDesk> desks,
        IDocumentManager<TicketTimeElapsedDocument> documents,
        IServiceProvider serviceProvider,
        IClock clock)
    {
        _workflowTypes = workflowTypes;
        _workflowManager = workflowManager;
        _tickets = tickets;
        _desks = desks;
        _documents = documents;
        _serviceProvider = serviceProvider;
        _clock = clock;
    }

    public async Task<int> ScanAsync(CancellationToken cancellationToken = default)
    {
        var workflowTypes = (await _workflowTypes.GetByStartActivityAsync(TicketsConstants.Events.TimeElapsed)).Where(type => type.IsEnabled).ToList();

        if (workflowTypes.Count == 0)
        {
            return 0;
        }

        var now = _clock.UtcNow;
        var document = await _documents.GetOrCreateMutableAsync();
        var desks = (await _desks.GetAllAsync(cancellationToken)).ToDictionary(desk => desk.ItemId, StringComparer.Ordinal);
        var openHours = _serviceProvider.GetService<IOpenHoursService>();
        var started = 0;

        foreach (var workflowType in workflowTypes)
        {
            foreach (var activity in workflowType.Activities.Where(activity => activity.IsStart && activity.Name == TicketsConstants.Events.TimeElapsed))
            {
                var hours = TicketActivityProperties.Number(activity, nameof(TicketTimeElapsedEvent.Hours), 24);
                var since = TicketActivityProperties.Text(activity, nameof(TicketTimeElapsedEvent.Since), TicketTimeElapsedSince.Created);
                var businessHours = TicketActivityProperties.Flag(activity, nameof(TicketTimeElapsedEvent.BusinessHours));
                var categories = TicketActivityProperties.Strings(activity, nameof(TicketTimeElapsedEvent.Categories))
                    .Select(name => Enum.TryParse<TicketStatusCategory>(name, ignoreCase: true, out var category) ? category : (TicketStatusCategory?)null)
                    .OfType<TicketStatusCategory>()
                    .ToList();
                var deskIds = TicketActivityProperties.Strings(activity, nameof(TicketEventActivity.DeskIds));

                var (tickets, _) = await _tickets.ListAsync(new TicketScopedQuery
                {
                    Query = new TicketQuery
                    {
                        DeskIds = [.. deskIds],
                        Categories = categories.Count == 0 ? [.. _open] : categories,
                        Sort = TicketSort.Created,
                        Take = TicketsPerActivity,
                    },
                    AllDeskIds = deskIds.Count > 0 ? deskIds : [.. desks.Keys],
                }, cancellationToken);

                foreach (var ticket in tickets)
                {
                    if (!ticket.TryGet<TicketPart>(out var part) || TicketTimeElapsedSince.Of(since, part, ticket.CreatedUtc ?? now) is not { } moment)
                    {
                        continue;
                    }

                    var key = $"{workflowType.WorkflowTypeId}:{activity.ActivityId}:{ticket.ContentItemId}";

                    if (document.Started.TryGetValue(key, out var startedFor) && startedFor >= moment)
                    {
                        continue;
                    }

                    var elapsed = businessHours && openHours is not null && desks.TryGetValue(part.DeskId ?? string.Empty, out var desk) && !string.IsNullOrEmpty(desk.HoursScheduleId)
                        ? await openHours.GetOpenTimeBetweenAsync(desk.HoursScheduleId, moment, now, cancellationToken: cancellationToken)
                        : now - moment;

                    if (elapsed.TotalHours < hours)
                    {
                        continue;
                    }

                    document.Started[key] = moment;

                    var input = TicketWorkflowInput.Of(ticket);
                    input[TicketWorkflowInput.ActivityId] = activity.ActivityId;
                    input[TicketWorkflowInput.WorkflowTypeId] = workflowType.WorkflowTypeId;

                    await _workflowManager.TriggerEventAsync(TicketsConstants.Events.TimeElapsed, input, ticket.ContentItemId);
                    started++;
                }
            }
        }

        foreach (var key in document.Started.Where(pair => pair.Value < now - _remembered).Select(pair => pair.Key).ToList())
        {
            document.Started.Remove(key);
        }

        await _documents.UpdateAsync(document);

        return started;
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
