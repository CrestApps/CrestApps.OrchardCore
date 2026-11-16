using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows;
using CrestApps.OrchardCore.Tickets.Workflows;
using Json.Schema;

namespace CrestApps.OrchardCore.Tickets.Recipes.Workflows;

internal abstract class TicketEventSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected const string Input = "Input: TicketId, Number, Subject, DeskId, StatusId, StatusCategory, PriorityId, TeamId, AssigneeId, Requester, ContentType";

    protected override string Category => "Tickets";

    protected override IEnumerable<string> Outcomes => ["Done"];

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        yield return (nameof(TicketEventActivity.DeskIds), WorkflowActivitySchemaBuilders.StringArray("The ids of the desks the event starts for; empty for every desk."));

        foreach (var property in GetOwnProperties())
        {
            yield return property;
        }
    }

    protected virtual IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetOwnProperties()
        => [];
}

internal sealed class TicketCreatedEventSchema : TicketEventSchemaBase
{
    public override string Name => TicketsConstants.Events.Created;

    protected override string DisplayText => "Ticket Created";

    protected override string Description => $"Starts when a ticket is created. {Input}";
}

internal sealed class TicketUpdatedEventSchema : TicketEventSchemaBase
{
    public override string Name => TicketsConstants.Events.Updated;

    protected override string DisplayText => "Ticket Updated";

    protected override string Description => $"Starts when a ticket's values or fields change. {Input}, ChangedFields";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetOwnProperties()
        => [(nameof(TicketUpdatedEvent.ChangedFields), WorkflowActivitySchemaBuilders.StringArray("The changed fields the event starts for: Status, Priority, Desk, Team, Assignee, Subject; empty for any change."))];
}

internal sealed class TicketStatusChangedEventSchema : TicketEventSchemaBase
{
    public override string Name => TicketsConstants.Events.StatusChanged;

    protected override string DisplayText => "Ticket Status Changed";

    protected override string Description => $"Starts when a ticket moves to another status. {Input}, Previous (the previous status id)";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetOwnProperties()
        => [(nameof(TicketStatusChangedEvent.Categories), WorkflowActivitySchemaBuilders.StringArray("The status categories a move into starts the event: New, Open, Pending, OnHold, Solved, Closed; empty for any move."))];
}

internal sealed class TicketAssignedEventSchema : TicketEventSchemaBase
{
    public override string Name => TicketsConstants.Events.Assigned;

    protected override string DisplayText => "Ticket Assigned";

    protected override string Description => $"Starts when a ticket is given to another assignee. {Input}, Previous (the previous assignee)";
}

internal sealed class TicketCustomerRepliedEventSchema : TicketEventSchemaBase
{
    public override string Name => TicketsConstants.Events.CustomerReplied;

    protected override string DisplayText => "Ticket Customer Replied";

    protected override string Description => $"Starts when the requester replies. {Input}, Body";
}

internal sealed class TicketAgentRepliedEventSchema : TicketEventSchemaBase
{
    public override string Name => TicketsConstants.Events.AgentReplied;

    protected override string DisplayText => "Ticket Agent Replied";

    protected override string Description => $"Starts when an agent replies to the requester. {Input}, Body";
}

internal sealed class TicketNoteAddedEventSchema : TicketEventSchemaBase
{
    public override string Name => TicketsConstants.Events.NoteAdded;

    protected override string DisplayText => "Ticket Note Added";

    protected override string Description => $"Starts when an internal note is added. {Input}, Body";
}

internal sealed class TicketServiceLevelWarningEventSchema : TicketEventSchemaBase
{
    public override string Name => TicketsConstants.Events.ServiceLevelWarning;

    protected override string DisplayText => "Ticket Service Level Warning";

    protected override string Description => $"Starts when a ticket is close to missing a service-level target. {Input}, Metric";
}

internal sealed class TicketServiceLevelBreachedEventSchema : TicketEventSchemaBase
{
    public override string Name => TicketsConstants.Events.ServiceLevelBreached;

    protected override string DisplayText => "Ticket Service Level Breached";

    protected override string Description => $"Starts when a ticket misses a service-level target. {Input}, Metric";
}

internal sealed class TicketSolvedEventSchema : TicketEventSchemaBase
{
    public override string Name => TicketsConstants.Events.Solved;

    protected override string DisplayText => "Ticket Solved";

    protected override string Description => $"Starts when a ticket is solved. {Input}";
}

internal sealed class TicketReopenedEventSchema : TicketEventSchemaBase
{
    public override string Name => TicketsConstants.Events.Reopened;

    protected override string DisplayText => "Ticket Reopened";

    protected override string Description => $"Starts when a solved or closed ticket is reopened. {Input}";
}

internal sealed class TicketClosedEventSchema : TicketEventSchemaBase
{
    public override string Name => TicketsConstants.Events.Closed;

    protected override string DisplayText => "Ticket Closed";

    protected override string Description => $"Starts when a ticket is closed. {Input}";
}

internal sealed class TicketMergedEventSchema : TicketEventSchemaBase
{
    public override string Name => TicketsConstants.Events.Merged;

    protected override string DisplayText => "Ticket Merged";

    protected override string Description => $"Starts when a ticket is merged into another. {Input}, MergedIntoId";
}

internal sealed class TicketRatedEventSchema : TicketEventSchemaBase
{
    public override string Name => TicketsConstants.Events.Rated;

    protected override string DisplayText => "Ticket Rated";

    protected override string Description => $"Starts when a requester rates a solved ticket. {Input}, Score (1 to 5), Comment";
}

internal sealed class TicketActionRequestedEventSchema : TicketEventSchemaBase
{
    public override string Name => TicketsConstants.Events.ActionRequested;

    protected override string DisplayText => "Ticket Action Requested";

    protected override string Description => $"Starts when an agent runs the named action from a ticket's page. {Input}, ActionName";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetOwnProperties()
        => [(nameof(TicketActionRequestedEvent.ActionName), WorkflowActivitySchemaBuilders.String("The action's name, shown under Actions on the ticket page."))];
}

internal sealed class TicketTimeElapsedEventSchema : TicketEventSchemaBase
{
    public override string Name => TicketsConstants.Events.TimeElapsed;

    protected override string DisplayText => "Ticket Time Elapsed";

    protected override string Description => $"Starts once a time has passed since a moment in a ticket's life, once per ticket and moment. {Input}";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetOwnProperties()
        =>
        [
            (nameof(TicketTimeElapsedEvent.Hours), new JsonSchemaBuilder().Type(SchemaValueType.Number).Description("How many hours must pass.")),
            (nameof(TicketTimeElapsedEvent.Since), WorkflowActivitySchemaBuilders.String("What the time counts from: Created, LastCustomerReply, LastAgentReply or StatusChanged.")),
            (nameof(TicketTimeElapsedEvent.BusinessHours), new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Count only the desk's business hours.")),
            (nameof(TicketTimeElapsedEvent.Categories), WorkflowActivitySchemaBuilders.StringArray("The status categories a ticket must be in; empty for New, Open, Pending and OnHold.")),
        ];
}

internal abstract class TicketTaskSchemaBase : WorkflowActivitySchemaDefinitionBase
{
    protected override string Category => "Tickets";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected virtual bool NeedsTicket => true;

    protected abstract IEnumerable<(string Name, string Description)> Fields { get; }

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(WorkflowActivitySchemaContext context)
    {
        if (NeedsTicket)
        {
            yield return (nameof(TicketTaskActivity.TicketIdExpression), WorkflowActivitySchemaBuilders.String("Liquid giving the ticket's id; the default is {{ Workflow.Input.TicketId }}."));
        }

        foreach (var (name, description) in Fields)
        {
            yield return (name, WorkflowActivitySchemaBuilders.String(description));
        }
    }
}

internal sealed class CreateTicketTaskSchema : TicketTaskSchemaBase
{
    public override string Name => nameof(CreateTicketTask);

    protected override string DisplayText => "Create Ticket";

    protected override string Description => "Creates a ticket; its id goes to Workflow.Properties.TicketId";

    protected override bool NeedsTicket => false;

    protected override IEnumerable<(string Name, string Description)> Fields =>
    [
        ("Subject", "Liquid giving the subject. Required."),
        ("Body", "Liquid giving the first message."),
        ("DeskId", "The desk's id; empty for the ticket type's default."),
        ("PriorityId", "The priority's id; empty for the default."),
        ("ContentType", "Liquid giving the ticket type; empty for the default."),
        ("Requester", "Liquid giving the requester as Kind:Id."),
        ("RequesterName", "Liquid giving the name of a requester no party source knows."),
        ("RequesterEmail", "Liquid giving the e-mail of a requester no party source knows."),
    ];
}

internal sealed class UpdateTicketTaskSchema : TicketTaskSchemaBase
{
    public override string Name => nameof(UpdateTicketTask);

    protected override string DisplayText => "Update Ticket";

    protected override string Description => "Changes a ticket's status, priority, desk, team, assignee, subject or category";

    protected override IEnumerable<(string Name, string Description)> Fields =>
    [
        ("StatusId", "The status's id; empty keeps it."),
        ("PriorityId", "The priority's id; empty keeps it."),
        ("DeskId", "The desk's id; empty keeps it."),
        ("TeamId", "Liquid giving a team id; empty keeps it."),
        ("AssigneeId", "Liquid giving a user id; empty keeps it."),
        ("Subject", "Liquid giving the subject; empty keeps it."),
        ("CategoryId", "Liquid giving a category's term id; empty keeps it. Only the category changes."),
    ];
}

internal sealed class AssignTicketTaskSchema : TicketTaskSchemaBase
{
    public override string Name => nameof(AssignTicketTask);

    protected override string DisplayText => "Assign Ticket";

    protected override string Description => "Assigns a ticket to a team, an agent, or a team member through team assignment";

    protected override IEnumerable<(string Name, string Description)> Fields =>
    [
        ("TeamId", "Liquid giving a team id; empty keeps the team."),
        ("AssigneeId", "Liquid giving a user id."),
        ("Mode", "Without an agent: empty leaves it to an agent, TeamMember gives it to a team member with the desk's strategy."),
    ];
}

internal sealed class AddTicketReplyTaskSchema : TicketTaskSchemaBase
{
    public override string Name => nameof(AddTicketReplyTask);

    protected override string DisplayText => "Add Ticket Reply";

    protected override string Description => "Adds a reply to the requester, who is e-mailed it";

    protected override IEnumerable<(string Name, string Description)> Fields =>
    [
        ("Body", "Liquid giving the reply."),
        ("StatusId", "The status to set with it; empty keeps it."),
    ];
}

internal sealed class AddTicketNoteTaskSchema : TicketTaskSchemaBase
{
    public override string Name => nameof(AddTicketNoteTask);

    protected override string DisplayText => "Add Ticket Note";

    protected override string Description => "Adds an internal note";

    protected override IEnumerable<(string Name, string Description)> Fields => [("Body", "Liquid giving the note.")];
}

internal sealed class EscalateTicketTaskSchema : TicketTaskSchemaBase
{
    public override string Name => nameof(EscalateTicketTask);

    protected override string DisplayText => "Escalate Ticket";

    protected override string Description => "Raises a ticket's escalation level, and optionally moves it to a team and a priority";

    protected override IEnumerable<(string Name, string Description)> Fields =>
    [
        ("Level", "Liquid giving the level; empty raises it by one."),
        ("TeamId", "Liquid giving a team id; empty keeps the team."),
        ("PriorityId", "The priority's id; empty keeps it."),
    ];
}

internal sealed class GetTicketTaskSchema : TicketTaskSchemaBase
{
    public override string Name => nameof(GetTicketTask);

    protected override string DisplayText => "Get Ticket";

    protected override string Description => "Reads a ticket into Workflow.Properties.Ticket";

    protected override IEnumerable<string> Outcomes => ["Done", "Failed"];

    protected override IEnumerable<(string Name, string Description)> Fields => [];
}

internal sealed class FindTicketsTaskSchema : TicketTaskSchemaBase
{
    public override string Name => nameof(FindTicketsTask);

    protected override string DisplayText => "Find Tickets";

    protected override string Description => "Finds tickets into Workflow.Properties.TicketIds and TicketCount";

    protected override IEnumerable<string> Outcomes => ["Found", "None"];

    protected override bool NeedsTicket => false;

    protected override IEnumerable<(string Name, string Description)> Fields =>
    [
        ("Requester", "Liquid giving the requester as Kind:Id; empty for anyone."),
        ("DeskId", "The desk's id; empty for every desk."),
        ("State", "open, solved or all."),
    ];
}

internal sealed class CheckTicketServiceLevelTaskSchema : TicketTaskSchemaBase
{
    public override string Name => nameof(CheckTicketServiceLevelTask);

    protected override string DisplayText => "Check Ticket Service Level";

    protected override string Description => "Checks how a ticket stands against its service-level targets";

    protected override IEnumerable<string> Outcomes => ["OnTrack", "Warning", "Breached", "Paused", "None", "Failed"];

    protected override IEnumerable<(string Name, string Description)> Fields => [];
}

internal sealed class LinkTicketsTaskSchema : TicketTaskSchemaBase
{
    public override string Name => nameof(LinkTicketsTask);

    protected override string DisplayText => "Link Tickets";

    protected override string Description => "Links a ticket to another ticket by number, or to a record as Kind:Id";

    protected override IEnumerable<(string Name, string Description)> Fields =>
    [
        ("Record", "Liquid giving a ticket number or a record as Kind:Id."),
        ("Kind", "Related, Parent, Child or Duplicate."),
    ];
}

internal sealed class CreateTicketWorkOrderTaskSchema : TicketTaskSchemaBase
{
    public override string Name => nameof(CrestApps.OrchardCore.Tickets.WorkOrders.CreateTicketWorkOrderTask);

    protected override string DisplayText => "Create Work Order From Ticket";

    protected override string Description => "Creates a work order from a ticket, with the ticket's subject, first message, account and requester, and links the ticket to it";

    protected override IEnumerable<(string Name, string Description)> Fields =>
    [
        ("Type", "Liquid giving the work order type's name or id."),
        ("Priority", "Low, Normal, High or Urgent; empty for the type's."),
    ];
}

internal sealed class TriageTicketTaskSchema : TicketTaskSchemaBase
{
    public override string Name => nameof(CrestApps.OrchardCore.Tickets.AI.TriageTicketTask);

    protected override string DisplayText => "Triage Ticket With AI";

    protected override string Description => "Adds an internal note with a summary and a priority from the ticket desk's AI assistant";

    protected override IEnumerable<(string Name, string Description)> Fields => [];
}

internal sealed class NotifyTicketTaskSchema : TicketTaskSchemaBase
{
    public override string Name => nameof(NotifyTicketTask);

    protected override string DisplayText => "Notify About Ticket";

    protected override string Description => "E-mails the requester, or tells the assignee through Orchard's notifications";

    protected override IEnumerable<(string Name, string Description)> Fields =>
    [
        ("To", "Requester or Assignee."),
        ("Subject", "Liquid giving the subject."),
        ("Body", "Liquid giving the message."),
    ];
}
