using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;

namespace CrestApps.OrchardCore.Tickets.Workflows;

/// <summary>
/// A workflow event about a ticket. It can be narrowed to some desks; its input names the ticket
/// (<see cref="TicketWorkflowInput"/>).
/// </summary>
public abstract class TicketEventActivity : EventActivity
{
    protected TicketEventActivity(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    protected IStringLocalizer S { get; }

    /// <inheritdoc/>
    public override LocalizedString Category => S["Tickets"];

    /// <summary>
    /// Gets or sets the desks the event starts for; empty for every desk.
    /// </summary>
    public IList<string> DeskIds
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"])];

    /// <inheritdoc/>
    public override bool CanExecute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => (DeskIds.Count == 0 || (workflowContext.Input.TryGetValue(TicketWorkflowInput.DeskId, out var deskId) && deskId is string id && DeskIds.Contains(id, StringComparer.Ordinal)))
            && Matches(workflowContext);

    /// <summary>
    /// Whether the event's own settings let it start for this input.
    /// </summary>
    /// <param name="workflowContext">The workflow context, with the input.</param>
    /// <returns><see langword="true"/> to start.</returns>
    protected virtual bool Matches(WorkflowExecutionContext workflowContext)
        => true;

    /// <inheritdoc/>
    public override ActivityExecutionResult Execute(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");

    /// <inheritdoc/>
    public override ActivityExecutionResult Resume(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => Outcome("Done");
}

/// <summary>Starts when a ticket is created.</summary>
public sealed class TicketCreatedEvent : TicketEventActivity
{
    public TicketCreatedEvent(IStringLocalizer<TicketCreatedEvent> stringLocalizer) : base(stringLocalizer) { }

    public override string Name => TicketsConstants.Events.Created;

    public override LocalizedString DisplayText => S["Ticket Created"];
}

/// <summary>Starts when a ticket's values or fields change, optionally only when some of them change.</summary>
public sealed class TicketUpdatedEvent : TicketEventActivity
{
    public TicketUpdatedEvent(IStringLocalizer<TicketUpdatedEvent> stringLocalizer) : base(stringLocalizer) { }

    public override string Name => TicketsConstants.Events.Updated;

    public override LocalizedString DisplayText => S["Ticket Updated"];

    /// <summary>
    /// Gets or sets the changed fields the event starts for (status, priority, desk, team, assignee, subject); empty for
    /// any change.
    /// </summary>
    public IList<string> ChangedFields
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    protected override bool Matches(WorkflowExecutionContext workflowContext)
        => ChangedFields.Count == 0
            || (workflowContext.Input.TryGetValue(TicketWorkflowInput.ChangedFields, out var changed) && changed is IEnumerable<string> fields
                && fields.Any(field => ChangedFields.Contains(field, StringComparer.OrdinalIgnoreCase)));
}

/// <summary>Starts when a ticket moves to another status, optionally only into some status categories.</summary>
public sealed class TicketStatusChangedEvent : TicketEventActivity
{
    public TicketStatusChangedEvent(IStringLocalizer<TicketStatusChangedEvent> stringLocalizer) : base(stringLocalizer) { }

    public override string Name => TicketsConstants.Events.StatusChanged;

    public override LocalizedString DisplayText => S["Ticket Status Changed"];

    /// <summary>
    /// Gets or sets the categories a ticket moves into that start the event; empty for any.
    /// </summary>
    public IList<string> Categories
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    protected override bool Matches(WorkflowExecutionContext workflowContext)
        => Categories.Count == 0
            || (workflowContext.Input.TryGetValue(TicketWorkflowInput.StatusCategory, out var category) && category is string name && Categories.Contains(name, StringComparer.OrdinalIgnoreCase));
}

/// <summary>Starts when a ticket is given to another assignee.</summary>
public sealed class TicketAssignedEvent : TicketEventActivity
{
    public TicketAssignedEvent(IStringLocalizer<TicketAssignedEvent> stringLocalizer) : base(stringLocalizer) { }

    public override string Name => TicketsConstants.Events.Assigned;

    public override LocalizedString DisplayText => S["Ticket Assigned"];
}

/// <summary>Starts when the requester replies.</summary>
public sealed class TicketCustomerRepliedEvent : TicketEventActivity
{
    public TicketCustomerRepliedEvent(IStringLocalizer<TicketCustomerRepliedEvent> stringLocalizer) : base(stringLocalizer) { }

    public override string Name => TicketsConstants.Events.CustomerReplied;

    public override LocalizedString DisplayText => S["Ticket Customer Replied"];
}

/// <summary>Starts when an agent replies to the requester.</summary>
public sealed class TicketAgentRepliedEvent : TicketEventActivity
{
    public TicketAgentRepliedEvent(IStringLocalizer<TicketAgentRepliedEvent> stringLocalizer) : base(stringLocalizer) { }

    public override string Name => TicketsConstants.Events.AgentReplied;

    public override LocalizedString DisplayText => S["Ticket Agent Replied"];
}

/// <summary>Starts when an internal note is added.</summary>
public sealed class TicketNoteAddedEvent : TicketEventActivity
{
    public TicketNoteAddedEvent(IStringLocalizer<TicketNoteAddedEvent> stringLocalizer) : base(stringLocalizer) { }

    public override string Name => TicketsConstants.Events.NoteAdded;

    public override LocalizedString DisplayText => S["Ticket Note Added"];
}

/// <summary>Starts when a ticket is close to missing a service-level target.</summary>
public sealed class TicketServiceLevelWarningEvent : TicketEventActivity
{
    public TicketServiceLevelWarningEvent(IStringLocalizer<TicketServiceLevelWarningEvent> stringLocalizer) : base(stringLocalizer) { }

    public override string Name => TicketsConstants.Events.ServiceLevelWarning;

    public override LocalizedString DisplayText => S["Ticket Service Level Warning"];
}

/// <summary>Starts when a ticket misses a service-level target.</summary>
public sealed class TicketServiceLevelBreachedEvent : TicketEventActivity
{
    public TicketServiceLevelBreachedEvent(IStringLocalizer<TicketServiceLevelBreachedEvent> stringLocalizer) : base(stringLocalizer) { }

    public override string Name => TicketsConstants.Events.ServiceLevelBreached;

    public override LocalizedString DisplayText => S["Ticket Service Level Breached"];
}

/// <summary>Starts when a ticket is solved.</summary>
public sealed class TicketSolvedEvent : TicketEventActivity
{
    public TicketSolvedEvent(IStringLocalizer<TicketSolvedEvent> stringLocalizer) : base(stringLocalizer) { }

    public override string Name => TicketsConstants.Events.Solved;

    public override LocalizedString DisplayText => S["Ticket Solved"];
}

/// <summary>Starts when a solved ticket is reopened.</summary>
public sealed class TicketReopenedEvent : TicketEventActivity
{
    public TicketReopenedEvent(IStringLocalizer<TicketReopenedEvent> stringLocalizer) : base(stringLocalizer) { }

    public override string Name => TicketsConstants.Events.Reopened;

    public override LocalizedString DisplayText => S["Ticket Reopened"];
}

/// <summary>Starts when a ticket is closed.</summary>
public sealed class TicketClosedEvent : TicketEventActivity
{
    public TicketClosedEvent(IStringLocalizer<TicketClosedEvent> stringLocalizer) : base(stringLocalizer) { }

    public override string Name => TicketsConstants.Events.Closed;

    public override LocalizedString DisplayText => S["Ticket Closed"];
}

/// <summary>Starts when a ticket is merged into another.</summary>
public sealed class TicketMergedEvent : TicketEventActivity
{
    public TicketMergedEvent(IStringLocalizer<TicketMergedEvent> stringLocalizer) : base(stringLocalizer) { }

    public override string Name => TicketsConstants.Events.Merged;

    public override LocalizedString DisplayText => S["Ticket Merged"];
}

/// <summary>
/// Starts when an agent runs a named action from a ticket's page; each action a published workflow starts with is
/// offered under <b>Actions</b> on the ticket.
/// </summary>
public sealed class TicketActionRequestedEvent : TicketEventActivity
{
    public TicketActionRequestedEvent(IStringLocalizer<TicketActionRequestedEvent> stringLocalizer) : base(stringLocalizer) { }

    public override string Name => TicketsConstants.Events.ActionRequested;

    public override LocalizedString DisplayText => S["Ticket Action Requested"];

    /// <summary>
    /// Gets or sets the action's name, as the ticket page shows it.
    /// </summary>
    public string ActionName
    {
        get => GetProperty<string>();
        set => SetProperty(value);
    }

    protected override bool Matches(WorkflowExecutionContext workflowContext)
        => workflowContext.Input.TryGetValue(TicketWorkflowInput.ActionName, out var name) && name is string action
            && string.Equals(action, ActionName?.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Starts once a time has passed since a moment in a ticket's life (created, the requester's last reply, the last agent
/// reply, the last status change), in clock hours or in the desk's business hours, for tickets in chosen status
/// categories. A background task checks every 15 minutes and starts the event once per ticket and moment.
/// </summary>
public sealed class TicketTimeElapsedEvent : TicketEventActivity
{
    public TicketTimeElapsedEvent(IStringLocalizer<TicketTimeElapsedEvent> stringLocalizer) : base(stringLocalizer) { }

    public override string Name => TicketsConstants.Events.TimeElapsed;

    public override LocalizedString DisplayText => S["Ticket Time Elapsed"];

    /// <summary>
    /// Gets or sets how many hours must pass.
    /// </summary>
    public double Hours
    {
        get => GetProperty(() => 24d);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the moment the time counts from (<see cref="TicketTimeElapsedSince"/>).
    /// </summary>
    public string Since
    {
        get => GetProperty(() => TicketTimeElapsedSince.Created);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets whether the time counts in the desk's business hours rather than clock hours.
    /// </summary>
    public bool BusinessHours
    {
        get => GetProperty(() => false);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets or sets the status categories a ticket must be in; empty for the ones still being worked on.
    /// </summary>
    public IList<string> Categories
    {
        get => GetProperty<IList<string>>(() => []);
        set => SetProperty(value);
    }

    protected override bool Matches(WorkflowExecutionContext workflowContext)
        => workflowContext.Input.TryGetValue(TicketWorkflowInput.ActivityId, out var activityId) && activityId is string id && workflowContext.Input.ContainsKey(TicketWorkflowInput.TicketId)
            && workflowContext.Input.TryGetValue(TicketWorkflowInput.WorkflowTypeId, out var typeId) && typeId is string type
            && string.Equals(type, workflowContext.WorkflowType.WorkflowTypeId, StringComparison.Ordinal)
            && workflowContext.WorkflowType.Activities.Any(activity => activity.ActivityId == id && activity.Name == Name);
}

/// <summary>
/// The moments <see cref="TicketTimeElapsedEvent"/> counts from.
/// </summary>
public static class TicketTimeElapsedSince
{
    /// <summary>When the ticket was created.</summary>
    public const string Created = "Created";

    /// <summary>The requester's last reply.</summary>
    public const string LastCustomerReply = "LastCustomerReply";

    /// <summary>The last agent reply.</summary>
    public const string LastAgentReply = "LastAgentReply";

    /// <summary>The last status change.</summary>
    public const string StatusChanged = "StatusChanged";

    /// <summary>
    /// The moment of a ticket, or <see langword="null"/> when it hasn't happened.
    /// </summary>
    /// <param name="since">The moment's name.</param>
    /// <param name="part">The ticket's part.</param>
    /// <param name="createdUtc">When the ticket was created.</param>
    /// <returns>The time, or <see langword="null"/>.</returns>
    public static DateTime? Of(string since, TicketPart part, DateTime createdUtc)
        => since switch
        {
            LastCustomerReply => part.LastCustomerActivityUtc,
            LastAgentReply => part.LastAgentActivityUtc,
            StatusChanged => part.StatusChangedUtc,
            _ => createdUtc,
        };
}
