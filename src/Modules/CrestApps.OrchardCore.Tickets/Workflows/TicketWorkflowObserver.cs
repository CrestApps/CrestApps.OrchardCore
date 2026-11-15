using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tickets.Workflows;

/// <summary>
/// The input every ticket workflow event carries.
/// </summary>
public static class TicketWorkflowInput
{
    /// <summary>The ticket's content item id.</summary>
    public const string TicketId = "TicketId";

    /// <summary>The ticket's number.</summary>
    public const string Number = "Number";

    /// <summary>The ticket's subject.</summary>
    public const string Subject = "Subject";

    /// <summary>The ticket's desk.</summary>
    public const string DeskId = "DeskId";

    /// <summary>The ticket's status.</summary>
    public const string StatusId = "StatusId";

    /// <summary>The ticket's status category.</summary>
    public const string StatusCategory = "StatusCategory";

    /// <summary>The ticket's priority.</summary>
    public const string PriorityId = "PriorityId";

    /// <summary>The ticket's team.</summary>
    public const string TeamId = "TeamId";

    /// <summary>The ticket's assignee.</summary>
    public const string AssigneeId = "AssigneeId";

    /// <summary>The ticket's requester, as <c>Kind:Id</c>.</summary>
    public const string Requester = "Requester";

    /// <summary>The ticket type.</summary>
    public const string ContentType = "ContentType";

    /// <summary>The ticket's category (a term id).</summary>
    public const string CategoryId = "CategoryId";

    /// <summary>The ticket's category and those above it, as <c>/top/child/category/</c>.</summary>
    public const string CategoryPath = "CategoryPath";

    /// <summary>The fields an update changed.</summary>
    public const string ChangedFields = "ChangedFields";

    /// <summary>The previous assignee or status.</summary>
    public const string Previous = "Previous";

    /// <summary>The text of a reply or note.</summary>
    public const string Body = "Body";

    /// <summary>The service-level target.</summary>
    public const string Metric = "Metric";

    /// <summary>The action an agent ran.</summary>
    public const string ActionName = "ActionName";

    /// <summary>The ticket another was merged into.</summary>
    public const string MergedIntoId = "MergedIntoId";

    /// <summary>The requester's score, from 1 to 5.</summary>
    public const string Score = "Score";

    /// <summary>What the requester added to their rating.</summary>
    public const string Comment = "Comment";

    /// <summary>The time-elapsed activity a scan started.</summary>
    public const string ActivityId = "ActivityId";

    /// <summary>The workflow type a time-elapsed scan started.</summary>
    public const string WorkflowTypeId = "WorkflowTypeId";

    /// <summary>
    /// The input of a ticket.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <returns>The input, to add the event's own values to.</returns>
    public static Dictionary<string, object> Of(ContentItem ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        ticket.TryGet<TicketPart>(out var part);
        part ??= new TicketPart();

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [TicketId] = ticket.ContentItemId,
            [Number] = part.Number,
            [Subject] = ticket.DisplayText,
            [DeskId] = part.DeskId,
            [StatusId] = part.StatusId,
            [StatusCategory] = part.StatusCategory.ToString(),
            [PriorityId] = part.PriorityId,
            [TeamId] = part.TeamId,
            [AssigneeId] = part.AssigneeId,
            [Requester] = part.Requester.IsEmpty ? null : part.Requester.ToString(),
            [ContentType] = ticket.ContentType,
            [CategoryId] = part.CategoryId,
            [CategoryPath] = part.CategoryPath,
        };
    }
}

/// <summary>
/// How deep a chain of workflow-caused ticket changes has gone in this flow of work. A ticket task enters it around its
/// change; the observer starts no workflow past <see cref="MaxDepth"/>, so rules that change tickets can't loop.
/// </summary>
public static class TicketWorkflowDepth
{
    /// <summary>
    /// The deepest chain of workflow-caused changes that still starts workflows.
    /// </summary>
    public const int MaxDepth = 3;

    private static readonly AsyncLocal<int> _depth = new();

    /// <summary>
    /// Gets how many workflow-caused changes the current change is inside.
    /// </summary>
    public static int Current => _depth.Value;

    /// <summary>
    /// Marks the start of a workflow-caused change, until the returned scope is disposed.
    /// </summary>
    /// <returns>The scope.</returns>
    public static IDisposable Enter()
    {
        _depth.Value++;

        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _depth.Value--;
            }
        }
    }
}

/// <summary>
/// Starts the ticket workflow events from what the ticket service tells its observers.
/// </summary>
internal sealed class TicketWorkflowObserver : ITicketObserver
{
    private readonly IWorkflowManager _workflowManager;
    private readonly ILogger _logger;

    public TicketWorkflowObserver(IWorkflowManager workflowManager, ILogger<TicketWorkflowObserver> logger)
    {
        _workflowManager = workflowManager;
        _logger = logger;
    }

    public Task CreatedAsync(ContentItem ticket, CancellationToken cancellationToken = default)
        => TriggerAsync(TicketsConstants.Events.Created, ticket);

    public Task UpdatedAsync(ContentItem ticket, IReadOnlyList<TimelineFieldChange> changes, CancellationToken cancellationToken = default)
        => TriggerAsync(TicketsConstants.Events.Updated, ticket, input => input[TicketWorkflowInput.ChangedFields] = changes.Select(change => change.Key ?? change.Field).ToArray());

    public Task RepliedAsync(ContentItem ticket, TimelineEntry entry, bool fromRequester, CancellationToken cancellationToken = default)
    {
        var name = entry?.Visibility == TimelineVisibility.Internal
            ? TicketsConstants.Events.NoteAdded
            : fromRequester ? TicketsConstants.Events.CustomerReplied : TicketsConstants.Events.AgentReplied;

        return TriggerAsync(name, ticket, input => input[TicketWorkflowInput.Body] = entry?.Body);
    }

    public Task MergedAsync(ContentItem ticket, ContentItem into, CancellationToken cancellationToken = default)
        => TriggerAsync(TicketsConstants.Events.Merged, ticket, input => input[TicketWorkflowInput.MergedIntoId] = into?.ContentItemId);

    public Task RatedAsync(ContentItem ticket, TicketSatisfaction satisfaction, CancellationToken cancellationToken = default)
        => TriggerAsync(TicketsConstants.Events.Rated, ticket, input =>
        {
            input[TicketWorkflowInput.Score] = satisfaction?.Score;
            input[TicketWorkflowInput.Comment] = satisfaction?.Comment;
        });

    public Task AssignedAsync(ContentItem ticket, string previousAssigneeId, CancellationToken cancellationToken = default)
        => TriggerAsync(TicketsConstants.Events.Assigned, ticket, input => input[TicketWorkflowInput.Previous] = previousAssigneeId);

    public async Task StatusChangedAsync(ContentItem ticket, TicketStatus previous, TicketStatus current, CancellationToken cancellationToken = default)
    {
        await TriggerAsync(TicketsConstants.Events.StatusChanged, ticket, input => input[TicketWorkflowInput.Previous] = previous?.ItemId);

        var moved = previous?.Category != current?.Category;

        if (moved && current?.Category == TicketStatusCategory.Solved)
        {
            await TriggerAsync(TicketsConstants.Events.Solved, ticket);
        }
        else if (moved && current?.Category == TicketStatusCategory.Closed)
        {
            await TriggerAsync(TicketsConstants.Events.Closed, ticket);
        }
        else if (moved && previous?.Category is TicketStatusCategory.Solved or TicketStatusCategory.Closed
            && current?.Category is TicketStatusCategory.New or TicketStatusCategory.Open or TicketStatusCategory.Pending or TicketStatusCategory.OnHold)
        {
            await TriggerAsync(TicketsConstants.Events.Reopened, ticket);
        }
    }

    public Task ServiceLevelAsync(ContentItem ticket, string metric, bool breached, CancellationToken cancellationToken = default)
        => TriggerAsync(breached ? TicketsConstants.Events.ServiceLevelBreached : TicketsConstants.Events.ServiceLevelWarning, ticket, input => input[TicketWorkflowInput.Metric] = metric);

    private Task TriggerAsync(string name, ContentItem ticket, Action<Dictionary<string, object>> add = null)
    {
        if (TicketWorkflowDepth.Current >= TicketWorkflowDepth.MaxDepth)
        {
            _logger.LogWarning("A chain of workflow-caused ticket changes reached {Depth} steps; the '{Event}' event wasn't started, so rules can't loop.", TicketWorkflowDepth.MaxDepth, name);

            return Task.CompletedTask;
        }

        var input = TicketWorkflowInput.Of(ticket);
        add?.Invoke(input);

        return _workflowManager.TriggerEventAsync(name, input, ticket.ContentItemId);
    }
}
