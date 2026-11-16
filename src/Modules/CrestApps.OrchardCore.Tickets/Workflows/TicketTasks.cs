using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.ServiceLevels.Models;
using CrestApps.OrchardCore.ServiceLevels.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Workflows.Abstractions.Models;
using OrchardCore.Workflows.Activities;
using OrchardCore.Workflows.Models;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Tickets.Workflows;

/// <summary>
/// The kinds of value a ticket task's field holds.
/// </summary>
public enum TicketTaskFieldKind
{
    /// <summary>A line of Liquid.</summary>
    Liquid,

    /// <summary>Several lines of Liquid.</summary>
    LiquidMultiline,

    /// <summary>A ticket status.</summary>
    Status,

    /// <summary>A ticket priority.</summary>
    Priority,

    /// <summary>A desk.</summary>
    Desk,

    /// <summary>One of the task's own choices.</summary>
    Choice,
}

/// <summary>
/// A field of a ticket task, as its editor shows it.
/// </summary>
/// <param name="Name">The property name, kept on the activity.</param>
/// <param name="Label">The label.</param>
/// <param name="Hint">The hint.</param>
/// <param name="Kind">The kind of value.</param>
/// <param name="Choices">The task's own choices, for <see cref="TicketTaskFieldKind.Choice"/>: value and text.</param>
public sealed record TicketTaskField(string Name, LocalizedString Label, LocalizedString Hint, TicketTaskFieldKind Kind, IReadOnlyList<KeyValuePair<string, LocalizedString>> Choices = null);

/// <summary>
/// A workflow task about tickets. Its fields are kept as activity properties of the names its <see cref="Fields"/>
/// declare; Liquid fields are rendered with the workflow's values. A change it makes is a workflow's (its actor says so)
/// and counts toward the chain of workflow-caused changes that stops rules from looping.
/// </summary>
public abstract class TicketTaskActivity : TaskActivity
{
    /// <summary>
    /// The default expression of the ticket a task works on: the ticket of the event that started the workflow.
    /// </summary>
    public const string DefaultTicketIdExpression = "{{ Workflow.Input.TicketId }}";

    protected TicketTaskActivity(IServiceProvider services, IWorkflowExpressionEvaluator evaluator, IStringLocalizer stringLocalizer)
    {
        Services = services;
        Evaluator = evaluator;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the services of the workflow's scope, for what the task needs only when it runs.
    /// </summary>
    protected IServiceProvider Services { get; }

    protected IWorkflowExpressionEvaluator Evaluator { get; }

    protected IStringLocalizer S { get; }

    /// <inheritdoc/>
    public override LocalizedString Category => S["Tickets"];

    /// <summary>
    /// Gets whether the task works on an existing ticket, named by <see cref="TicketIdExpression"/>.
    /// </summary>
    public virtual bool NeedsTicket => true;

    /// <summary>
    /// Gets or sets the Liquid that gives the ticket's id.
    /// </summary>
    public string TicketIdExpression
    {
        get => GetProperty(() => DefaultTicketIdExpression);
        set => SetProperty(value);
    }

    /// <summary>
    /// Gets the task's own fields.
    /// </summary>
    public abstract IReadOnlyList<TicketTaskField> Fields { get; }

    /// <summary>
    /// Reads a field.
    /// </summary>
    /// <param name="name">The field's name.</param>
    /// <returns>Its value, or <see langword="null"/>.</returns>
    public string GetValue(string name)
        => Properties?[name]?.ToString();

    /// <summary>
    /// Writes a field.
    /// </summary>
    /// <param name="name">The field's name.</param>
    /// <param name="value">Its value; empty removes it.</param>
    public void SetValue(string name, string value)
    {
        Properties ??= [];

        if (string.IsNullOrWhiteSpace(value))
        {
            Properties.Remove(name);
        }
        else
        {
            Properties[name] = value;
        }
    }

    /// <inheritdoc/>
    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Done"]), new Outcome(S["Failed"])];

    /// <inheritdoc/>
    public override async Task<ActivityExecutionResult> ExecuteAsync(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
    {
        var tickets = Services.GetRequiredService<ITicketService>();
        var ticketId = NeedsTicket ? await EvaluateAsync(TicketIdExpression, workflowContext) : null;

        if (NeedsTicket && (string.IsNullOrEmpty(ticketId) || await tickets.GetAsync(ticketId) is null))
        {
            workflowContext.LastResult = S["The ticket wasn't found."].Value;

            return Outcome("Failed");
        }

        using var depth = TicketWorkflowDepth.Enter();

        return await RunAsync(workflowContext, tickets, ticketId);
    }

    /// <summary>
    /// Does the task's work.
    /// </summary>
    /// <param name="workflowContext">The workflow context.</param>
    /// <param name="tickets">The ticket service.</param>
    /// <param name="ticketId">The ticket, when <see cref="NeedsTicket"/>.</param>
    /// <returns>The outcome.</returns>
    protected abstract Task<ActivityExecutionResult> RunAsync(WorkflowExecutionContext workflowContext, ITicketService tickets, string ticketId);

    /// <summary>
    /// Renders a field's Liquid; a choice field is returned as kept.
    /// </summary>
    /// <param name="name">The field's name.</param>
    /// <param name="workflowContext">The workflow context.</param>
    /// <returns>The value, trimmed, or <see langword="null"/> when empty.</returns>
    protected async Task<string> FieldAsync(string name, WorkflowExecutionContext workflowContext)
    {
        var field = Fields.FirstOrDefault(item => item.Name == name);
        var value = GetValue(name);

        return field?.Kind is TicketTaskFieldKind.Liquid or TicketTaskFieldKind.LiquidMultiline
            ? await EvaluateAsync(value, workflowContext)
            : string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>
    /// The actor of a workflow's change.
    /// </summary>
    protected TicketActor Actor
        => new() { Name = S["Workflow"], FromWorkflow = true };

    /// <summary>
    /// The outcome of a ticket change.
    /// </summary>
    /// <param name="workflowContext">The workflow context.</param>
    /// <param name="result">The result.</param>
    /// <returns>Done, or Failed with the reasons as the last result.</returns>
    protected static ActivityExecutionResult OutcomeOf(WorkflowExecutionContext workflowContext, TicketResult result)
    {
        if (result.Succeeded)
        {
            return Outcome("Done");
        }

        workflowContext.LastResult = string.Join(" ", result.Errors);

        return Outcome("Failed");
    }

    private async Task<string> EvaluateAsync(string expression, WorkflowExecutionContext workflowContext, System.Text.Encodings.Web.TextEncoder encoder = null)
        => string.IsNullOrWhiteSpace(expression)
            ? null
            : (await Evaluator.EvaluateAsync(new WorkflowExpression<string>(expression), workflowContext, encoder))?.Trim() is { Length: > 0 } value ? value : null;

    /// <summary>
    /// Reads a Liquid field for HTML, the values it inserts (what a requester typed, say) HTML-encoded.
    /// </summary>
    /// <param name="name">The field's name.</param>
    /// <param name="workflowContext">The workflow.</param>
    /// <returns>The HTML, or <see langword="null"/> when empty.</returns>
    protected Task<string> HtmlFieldAsync(string name, WorkflowExecutionContext workflowContext)
        => EvaluateAsync(GetValue(name), workflowContext, System.Text.Encodings.Web.HtmlEncoder.Default);
}

/// <summary>Creates a ticket.</summary>
public sealed class CreateTicketTask : TicketTaskActivity
{
    public CreateTicketTask(IServiceProvider services, IWorkflowExpressionEvaluator evaluator, IStringLocalizer<CreateTicketTask> stringLocalizer) : base(services, evaluator, stringLocalizer) { }

    public override string Name => nameof(CreateTicketTask);

    public override LocalizedString DisplayText => S["Create Ticket"];

    public override bool NeedsTicket => false;

    public override IReadOnlyList<TicketTaskField> Fields =>
    [
        new("Subject", S["Subject"], S["Liquid. Required."], TicketTaskFieldKind.Liquid),
        new("Body", S["First message"], S["Liquid. Optional."], TicketTaskFieldKind.LiquidMultiline),
        new("DeskId", S["Desk"], S["Empty for the ticket type's default desk."], TicketTaskFieldKind.Desk),
        new("PriorityId", S["Priority"], S["Empty for the ticket type's default."], TicketTaskFieldKind.Priority),
        new("ContentType", S["Ticket type"], S["Liquid. Empty for the default ticket type."], TicketTaskFieldKind.Liquid),
        new("Requester", S["Requester"], S["Liquid giving a party as Kind:Id, such as User:4x2...; optional."], TicketTaskFieldKind.Liquid),
        new("RequesterName", S["Requester's name"], S["Liquid, for a requester no party source knows."], TicketTaskFieldKind.Liquid),
        new("RequesterEmail", S["Requester's e-mail"], S["Liquid, for a requester no party source knows."], TicketTaskFieldKind.Liquid),
    ];

    protected override async Task<ActivityExecutionResult> RunAsync(WorkflowExecutionContext workflowContext, ITicketService tickets, string ticketId)
    {
        _ = Parties.Models.PartyRef.TryParse(await FieldAsync("Requester", workflowContext), out var requester);
        var name = await FieldAsync("RequesterName", workflowContext);
        var email = await FieldAsync("RequesterEmail", workflowContext);

        var result = await tickets.CreateAsync(new TicketCreateRequest
        {
            Subject = await FieldAsync("Subject", workflowContext),
            Body = await FieldAsync("Body", workflowContext),
            DeskId = await FieldAsync("DeskId", workflowContext),
            PriorityId = await FieldAsync("PriorityId", workflowContext),
            ContentType = await FieldAsync("ContentType", workflowContext),
            Requester = requester,
            RequesterContact = name is null && email is null ? null : new ContactInfo { Name = name, Email = email },
            Channel = "Workflow",
        });

        if (result.Succeeded)
        {
            workflowContext.Properties[TicketWorkflowInput.TicketId] = result.Ticket.ContentItemId;
            workflowContext.LastResult = result.Ticket.ContentItemId;
        }

        return OutcomeOf(workflowContext, result);
    }
}

/// <summary>Changes a ticket's status, priority, desk, team, assignee or subject.</summary>
public sealed class UpdateTicketTask : TicketTaskActivity
{
    public UpdateTicketTask(IServiceProvider services, IWorkflowExpressionEvaluator evaluator, IStringLocalizer<UpdateTicketTask> stringLocalizer) : base(services, evaluator, stringLocalizer) { }

    public override string Name => nameof(UpdateTicketTask);

    public override LocalizedString DisplayText => S["Update Ticket"];

    public override IReadOnlyList<TicketTaskField> Fields =>
    [
        new("StatusId", S["Status"], S["Empty keeps the status."], TicketTaskFieldKind.Status),
        new("PriorityId", S["Priority"], S["Empty keeps the priority."], TicketTaskFieldKind.Priority),
        new("DeskId", S["Desk"], S["Empty keeps the desk."], TicketTaskFieldKind.Desk),
        new("TeamId", S["Team"], S["Liquid giving a team id; empty keeps the team."], TicketTaskFieldKind.Liquid),
        new("AssigneeId", S["Assignee"], S["Liquid giving a user id; empty keeps the assignee."], TicketTaskFieldKind.Liquid),
        new("Subject", S["Subject"], S["Liquid; empty keeps the subject."], TicketTaskFieldKind.Liquid),
        new("CategoryId", S["Category"], S["Liquid giving a category's term id (one of the ticket type's categories); empty keeps the category. Only the category changes."], TicketTaskFieldKind.Liquid),
    ];

    protected override async Task<ActivityExecutionResult> RunAsync(WorkflowExecutionContext workflowContext, ITicketService tickets, string ticketId)
        => OutcomeOf(workflowContext, await tickets.UpdateAsync(ticketId, new TicketUpdate
        {
            CategoryId = await FieldAsync("CategoryId", workflowContext) is { Length: > 0 } categoryId ? categoryId.Trim() : null,
            StatusId = await FieldAsync("StatusId", workflowContext),
            PriorityId = await FieldAsync("PriorityId", workflowContext),
            DeskId = await FieldAsync("DeskId", workflowContext),
            TeamId = await FieldAsync("TeamId", workflowContext),
            AssigneeId = await FieldAsync("AssigneeId", workflowContext),
            Subject = await FieldAsync("Subject", workflowContext),
        }, Actor));
}

/// <summary>Assigns a ticket: to a team, to an agent, or to a member of its team through team assignment.</summary>
public sealed class AssignTicketTask : TicketTaskActivity
{
    public const string ToTeamMember = "TeamMember";

    public AssignTicketTask(IServiceProvider services, IWorkflowExpressionEvaluator evaluator, IStringLocalizer<AssignTicketTask> stringLocalizer) : base(services, evaluator, stringLocalizer) { }

    public override string Name => nameof(AssignTicketTask);

    public override LocalizedString DisplayText => S["Assign Ticket"];

    public override IReadOnlyList<TicketTaskField> Fields =>
    [
        new("TeamId", S["Team"], S["Liquid giving a team id; empty keeps the team."], TicketTaskFieldKind.Liquid),
        new("AssigneeId", S["Agent"], S["Liquid giving a user id. Empty, with team assignment below, lets the team choose."], TicketTaskFieldKind.Liquid),
        new("Mode", S["Then"], S["What happens without an agent."], TicketTaskFieldKind.Choice, [new(string.Empty, S["Leave it to an agent"]), new(ToTeamMember, S["Give it to a team member (desk's strategy)"])]),
    ];

    protected override async Task<ActivityExecutionResult> RunAsync(WorkflowExecutionContext workflowContext, ITicketService tickets, string ticketId)
    {
        var teamId = await FieldAsync("TeamId", workflowContext);
        var assigneeId = await FieldAsync("AssigneeId", workflowContext);

        if (teamId is not null || assigneeId is not null)
        {
            var updated = await tickets.UpdateAsync(ticketId, new TicketUpdate { TeamId = teamId, AssigneeId = assigneeId }, Actor);

            if (!updated.Succeeded || assigneeId is not null)
            {
                return OutcomeOf(workflowContext, updated);
            }
        }

        return GetValue("Mode") == ToTeamMember
            ? OutcomeOf(workflowContext, await tickets.AutoAssignAsync(ticketId, Actor))
            : Outcome("Done");
    }
}

/// <summary>Adds a reply to the requester.</summary>
public sealed class AddTicketReplyTask : TicketTaskActivity
{
    public AddTicketReplyTask(IServiceProvider services, IWorkflowExpressionEvaluator evaluator, IStringLocalizer<AddTicketReplyTask> stringLocalizer) : base(services, evaluator, stringLocalizer) { }

    public override string Name => nameof(AddTicketReplyTask);

    public override LocalizedString DisplayText => S["Add Ticket Reply"];

    public override IReadOnlyList<TicketTaskField> Fields =>
    [
        new("Body", S["Reply"], S["Liquid. The requester sees it and is e-mailed it."], TicketTaskFieldKind.LiquidMultiline),
        new("StatusId", S["Then set the status"], S["Empty keeps the status."], TicketTaskFieldKind.Status),
    ];

    protected override async Task<ActivityExecutionResult> RunAsync(WorkflowExecutionContext workflowContext, ITicketService tickets, string ticketId)
        => OutcomeOf(workflowContext, await tickets.ReplyAsync(ticketId, new TicketReply
        {
            Body = await FieldAsync("Body", workflowContext),
            AuthorName = S["Workflow"],
            Channel = "Workflow",
            SetStatusId = await FieldAsync("StatusId", workflowContext),
            Actor = Actor,
        }));
}

/// <summary>Adds an internal note.</summary>
public sealed class AddTicketNoteTask : TicketTaskActivity
{
    public AddTicketNoteTask(IServiceProvider services, IWorkflowExpressionEvaluator evaluator, IStringLocalizer<AddTicketNoteTask> stringLocalizer) : base(services, evaluator, stringLocalizer) { }

    public override string Name => nameof(AddTicketNoteTask);

    public override LocalizedString DisplayText => S["Add Ticket Note"];

    public override IReadOnlyList<TicketTaskField> Fields =>
    [
        new("Body", S["Note"], S["Liquid. Only agents see it."], TicketTaskFieldKind.LiquidMultiline),
    ];

    protected override async Task<ActivityExecutionResult> RunAsync(WorkflowExecutionContext workflowContext, ITicketService tickets, string ticketId)
        => OutcomeOf(workflowContext, await tickets.ReplyAsync(ticketId, new TicketReply
        {
            Body = await FieldAsync("Body", workflowContext),
            IsInternal = true,
            AuthorName = S["Workflow"],
            Channel = "Workflow",
            Actor = Actor,
        }));
}

/// <summary>Raises a ticket's escalation level, and optionally moves it to a team and a priority.</summary>
public sealed class EscalateTicketTask : TicketTaskActivity
{
    public EscalateTicketTask(IServiceProvider services, IWorkflowExpressionEvaluator evaluator, IStringLocalizer<EscalateTicketTask> stringLocalizer) : base(services, evaluator, stringLocalizer) { }

    public override string Name => nameof(EscalateTicketTask);

    public override LocalizedString DisplayText => S["Escalate Ticket"];

    public override IReadOnlyList<TicketTaskField> Fields =>
    [
        new("Level", S["Escalation level"], S["Liquid giving a number; empty raises the level by one."], TicketTaskFieldKind.Liquid),
        new("TeamId", S["Team"], S["Liquid giving a team id; empty keeps the team."], TicketTaskFieldKind.Liquid),
        new("PriorityId", S["Priority"], S["Empty keeps the priority."], TicketTaskFieldKind.Priority),
    ];

    protected override async Task<ActivityExecutionResult> RunAsync(WorkflowExecutionContext workflowContext, ITicketService tickets, string ticketId)
    {
        // With no level given the level goes up by one, counted inside the ticket's lock.
        var level = int.TryParse(await FieldAsync("Level", workflowContext), NumberStyles.Integer, CultureInfo.InvariantCulture, out var given) ? given : (int?)null;

        return OutcomeOf(workflowContext, await tickets.UpdateAsync(ticketId, new TicketUpdate
        {
            EscalationLevel = level,
            RaiseEscalation = level is null,
            TeamId = await FieldAsync("TeamId", workflowContext),
            PriorityId = await FieldAsync("PriorityId", workflowContext),
        }, Actor));
    }
}

/// <summary>Reads a ticket into the workflow's properties (<c>{{ Workflow.Properties.Ticket.Number }}</c> and so on).</summary>
public sealed class GetTicketTask : TicketTaskActivity
{
    public GetTicketTask(IServiceProvider services, IWorkflowExpressionEvaluator evaluator, IStringLocalizer<GetTicketTask> stringLocalizer) : base(services, evaluator, stringLocalizer) { }

    public override string Name => nameof(GetTicketTask);

    public override LocalizedString DisplayText => S["Get Ticket"];

    public override IReadOnlyList<TicketTaskField> Fields => [];

    protected override async Task<ActivityExecutionResult> RunAsync(WorkflowExecutionContext workflowContext, ITicketService tickets, string ticketId)
    {
        workflowContext.Properties["Ticket"] = TicketWorkflowInput.Of(await tickets.GetAsync(ticketId));

        return Outcome("Done");
    }
}

/// <summary>Finds tickets by requester, desk and state, into <c>{{ Workflow.Properties.TicketIds }}</c>.</summary>
public sealed class FindTicketsTask : TicketTaskActivity
{
    public FindTicketsTask(IServiceProvider services, IWorkflowExpressionEvaluator evaluator, IStringLocalizer<FindTicketsTask> stringLocalizer) : base(services, evaluator, stringLocalizer) { }

    public override string Name => nameof(FindTicketsTask);

    public override LocalizedString DisplayText => S["Find Tickets"];

    public override bool NeedsTicket => false;

    public override IReadOnlyList<TicketTaskField> Fields =>
    [
        new("Requester", S["Requester"], S["Liquid giving a party as Kind:Id; empty for anyone."], TicketTaskFieldKind.Liquid),
        new("DeskId", S["Desk"], S["Empty for every desk."], TicketTaskFieldKind.Desk),
        new("State", S["State"], null, TicketTaskFieldKind.Choice, [new("open", S["Still being worked on"]), new("solved", S["Solved"]), new("all", S["Any"])]),
    ];

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["Found"]), new Outcome(S["None"])];

    protected override async Task<ActivityExecutionResult> RunAsync(WorkflowExecutionContext workflowContext, ITicketService tickets, string ticketId)
    {
        var desks = await Services.GetRequiredService<CrestApps.Core.Services.ICatalog<TicketDesk>>().GetAllAsync();
        var deskId = await FieldAsync("DeskId", workflowContext);
        var (found, total) = await tickets.ListAsync(new TicketScopedQuery
        {
            Query = new TicketQuery
            {
                Requester = await FieldAsync("Requester", workflowContext),
                DeskIds = deskId is null ? [] : [deskId],
                Categories = GetValue("State") switch
                {
                    "solved" => [TicketStatusCategory.Solved],
                    "all" => [],
                    _ => [TicketStatusCategory.New, TicketStatusCategory.Open, TicketStatusCategory.Pending, TicketStatusCategory.OnHold],
                },
                Take = 100,
            },
            AllDeskIds = [.. desks.Select(desk => desk.ItemId)],
        });

        workflowContext.Properties["TicketIds"] = found.Select(ticket => ticket.ContentItemId).ToArray();
        workflowContext.Properties["TicketCount"] = total;

        return Outcome(total > 0 ? "Found" : "None");
    }
}

/// <summary>Checks how a ticket stands against its service-level targets.</summary>
public sealed class CheckTicketServiceLevelTask : TicketTaskActivity
{
    public CheckTicketServiceLevelTask(IServiceProvider services, IWorkflowExpressionEvaluator evaluator, IStringLocalizer<CheckTicketServiceLevelTask> stringLocalizer) : base(services, evaluator, stringLocalizer) { }

    public override string Name => nameof(CheckTicketServiceLevelTask);

    public override LocalizedString DisplayText => S["Check Ticket Service Level"];

    public override IReadOnlyList<TicketTaskField> Fields => [];

    public override IEnumerable<Outcome> GetPossibleOutcomes(WorkflowExecutionContext workflowContext, ActivityContext activityContext)
        => [new Outcome(S["OnTrack"]), new Outcome(S["Warning"]), new Outcome(S["Breached"]), new Outcome(S["Paused"]), new Outcome(S["None"]), new Outcome(S["Failed"])];

    protected override async Task<ActivityExecutionResult> RunAsync(WorkflowExecutionContext workflowContext, ITicketService tickets, string ticketId)
    {
        var tracker = Services.GetRequiredService<IServiceLevelTracker>();
        var timers = await tracker.GetTimersAsync(new RecordRef(TicketsConstants.RecordKind, ticketId), activeOnly: true);

        return Outcome(StandingOf(timers));
    }

    /// <summary>
    /// How a ticket stands from its active timers: breached, close to a target, paused, on track, or without targets.
    /// </summary>
    /// <param name="timers">The active timers.</param>
    /// <returns>The outcome's name.</returns>
    public static string StandingOf(IReadOnlyCollection<ServiceLevelTimer> timers)
        => timers.Count == 0 ? "None"
            : timers.Any(timer => timer.BreachedUtc is not null) ? "Breached"
            : timers.Any(timer => timer.WarnedUtc is not null) ? "Warning"
            : timers.All(timer => timer.State == ServiceLevelTimerState.Paused) ? "Paused"
            : "OnTrack";
}

/// <summary>Links a ticket to another record: a ticket by its number, or any record as Kind:Id.</summary>
public sealed class LinkTicketsTask : TicketTaskActivity
{
    public LinkTicketsTask(IServiceProvider services, IWorkflowExpressionEvaluator evaluator, IStringLocalizer<LinkTicketsTask> stringLocalizer) : base(services, evaluator, stringLocalizer) { }

    public override string Name => nameof(LinkTicketsTask);

    public override LocalizedString DisplayText => S["Link Tickets"];

    public override IReadOnlyList<TicketTaskField> Fields =>
    [
        new("Record", S["Link to"], S["Liquid giving a ticket number (T-1001) or a record as Kind:Id."], TicketTaskFieldKind.Liquid),
        new("Kind", S["How they relate"], null, TicketTaskFieldKind.Choice,
            [new(TicketLinkKinds.Related, S["Related"]), new(TicketLinkKinds.Parent, S["Parent of this ticket"]), new(TicketLinkKinds.Child, S["Part of this ticket"]), new(TicketLinkKinds.Duplicate, S["Duplicate"])]),
    ];

    protected override async Task<ActivityExecutionResult> RunAsync(WorkflowExecutionContext workflowContext, ITicketService tickets, string ticketId)
    {
        var target = await FieldAsync("Record", workflowContext);

        if (!RecordRef.TryParse(target, out var record))
        {
            record = await tickets.FindByNumberAsync(target) is { } other ? new RecordRef(TicketsConstants.RecordKind, other.ContentItemId) : default;
        }

        return OutcomeOf(workflowContext, await tickets.LinkAsync(ticketId, record, GetValue("Kind") ?? TicketLinkKinds.Related, Actor));
    }
}

/// <summary>E-mails the requester, or tells the assignee through Orchard's notifications.</summary>
public sealed class NotifyTicketTask : TicketTaskActivity
{
    public const string Requester = "Requester";
    public const string Assignee = "Assignee";

    public NotifyTicketTask(IServiceProvider services, IWorkflowExpressionEvaluator evaluator, IStringLocalizer<NotifyTicketTask> stringLocalizer) : base(services, evaluator, stringLocalizer) { }

    public override string Name => nameof(NotifyTicketTask);

    public override LocalizedString DisplayText => S["Notify About Ticket"];

    public override IReadOnlyList<TicketTaskField> Fields =>
    [
        new("To", S["Who"], null, TicketTaskFieldKind.Choice, [new(Requester, S["The requester, by e-mail"]), new(Assignee, S["The assignee, by notification"])]),
        new("Subject", S["Subject"], S["Liquid."], TicketTaskFieldKind.Liquid),
        new("Body", S["Message"], S["Liquid. HTML for the requester's e-mail, plain text for a notification."], TicketTaskFieldKind.LiquidMultiline),
    ];

    protected override async Task<ActivityExecutionResult> RunAsync(WorkflowExecutionContext workflowContext, ITicketService tickets, string ticketId)
    {
        var services = Services;
        var ticket = await tickets.GetAsync(ticketId);
        ticket.TryGet<TicketPart>(out var part);
        var subject = await FieldAsync("Subject", workflowContext) ?? $"{part?.Number} {ticket.DisplayText}";
        var toAssignee = GetValue("To") == Assignee;
        var body = (toAssignee ? await FieldAsync("Body", workflowContext) : await HtmlFieldAsync("Body", workflowContext)) ?? string.Empty;

        if (toAssignee)
        {
            if (string.IsNullOrEmpty(part?.AssigneeId)
                || services.GetService<global::OrchardCore.Notifications.INotificationService>() is not { } notifications
                || await services.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<global::OrchardCore.Users.IUser>>().FindByIdAsync(part.AssigneeId) is not { } user)
            {
                return Outcome("Failed");
            }

            var message = new global::OrchardCore.Notifications.Models.NotificationMessage
            {
                Subject = subject,
                Summary = WebUtility.HtmlEncode(subject),
                TextBody = body,
                HtmlBody = WebUtility.HtmlEncode(body),
                Type = TicketsConstants.NotificationTypes.Workflow,
                ReferenceId = ticket.ContentItemId,
                Url = services.GetService<Notifications.TicketNotificationSender>()?.AdminUrl(ticket.ContentItemId),
            };
            message.Put(new Notifications.TicketNotificationInfo { TicketId = ticket.ContentItemId, Number = part.Number, Title = ticket.DisplayText, DeskId = part.DeskId });

            await notifications.SendAsync(user, message);

            return Outcome("Done");
        }

        var email = part?.RequesterContact?.Email;

        if (string.IsNullOrWhiteSpace(email) && part?.Requester.IsEmpty == false)
        {
            email = (await services.GetRequiredService<Parties.Services.IPartyDirectory>().GetContactInfoAsync(part.Requester))?.Email;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return Outcome("Failed");
        }

        var sent = await services.GetRequiredService<ITemplatedEmailSender>().SendAsync(
            new TemplatedEmail { To = email.Trim(), Reference = part?.Number },
            _ => Task.FromResult(new TemplatedEmailContent { Subject = subject, Html = body }));

        return Outcome(sent ? "Done" : "Failed");
    }
}
