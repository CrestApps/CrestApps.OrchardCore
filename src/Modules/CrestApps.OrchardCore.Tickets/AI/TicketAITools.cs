using CrestApps.Core.Security;
using System.Security.Claims;
using System.Text.Json;
using CrestApps.Core.AI.Extensions;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets.Core.Api;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.AI;

/// <summary>
/// The base of the ticket AI tools: each acts as the signed-in user, within the tickets they may view and the actions
/// they may take, and answers in JSON.
/// </summary>
public abstract class TicketAITool : AIFunction
{
    protected static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly JsonElement _jsonSchema;

    protected TicketAITool(string properties, params string[] required)
    {
        var requiredList = required.Length == 0 ? string.Empty : $$""","required":{{JsonSerializer.Serialize(required)}}""";
        _jsonSchema = JsonSerializer.Deserialize<JsonElement>($$"""{"type":"object","properties":{{{properties}}}{{requiredList}},"additionalProperties":false}""");
    }

    public override JsonElement JsonSchema => _jsonSchema;

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object> { ["Strict"] = false };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if ((arguments.Services.GetService<IUserAccessor>()?.User ?? arguments.Services.GetService<IHttpContextAccessor>()?.HttpContext?.User) is not { Identity.IsAuthenticated: true } user)
        {
            return "Only a signed-in user can work with tickets.";
        }

        return await InvokeAsync(arguments, user, cancellationToken);
    }

    protected abstract Task<object> InvokeAsync(AIFunctionArguments arguments, ClaimsPrincipal user, CancellationToken cancellationToken);

    protected static string Text(AIFunctionArguments arguments, string name)
        => arguments.TryGetFirstString(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    protected static async Task<ContentItem> FindVisibleAsync(AIFunctionArguments arguments, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var id = Text(arguments, "ticket");
        var tickets = arguments.Services.GetRequiredService<ITicketService>();
        var ticket = id is null ? null : await tickets.GetAsync(id) ?? await tickets.FindByNumberAsync(id);

        return ticket is not null && await arguments.Services.GetRequiredService<ITicketAccessService>().CanAsync(user, ticket, TicketOperation.ViewAssigned, cancellationToken)
            ? ticket
            : null;
    }

    protected static string Json(object value)
        => JsonSerializer.Serialize(value, JsonOptions);

    protected static PartyRef CallerOf(ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.NameIdentifier) is { Length: > 0 } userId ? new PartyRef(PartyKinds.User, userId) : default;
}

/// <summary>
/// Lists the desks, statuses and priorities, so the model can turn names into ids.
/// </summary>
public sealed class ListTicketOptionsTool : TicketAITool
{
    public const string TheName = "listTicketOptions";

    public ListTicketOptionsTool()
        : base(string.Empty)
    {
    }

    public override string Name => TheName;

    public override string Description => "Lists the ticket desks the user may open tickets in, every status (with its category: New, Open, Pending, OnHold, Solved or Closed) and priority, and the categories of tickets (such as Hardware > Laptop), each with the id the other ticket tools take.";

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var services = arguments.Services;
        var authorization = services.GetRequiredService<IAuthorizationService>();
        var desks = new List<object>();

        foreach (var desk in await services.GetRequiredService<ICatalog<TicketDesk>>().GetAllAsync(cancellationToken))
        {
            if (await authorization.AuthorizeAsync(user, TicketsPermissions.ForDesk(TicketOperation.Reply, desk)))
            {
                desks.Add(new { id = desk.ItemId, name = desk.Name });
            }
        }

        var statuses = (await services.GetRequiredService<ICatalog<TicketStatus>>().GetAllAsync(cancellationToken))
            .OrderBy(status => status.Order)
            .Select(status => new { id = status.ItemId, name = status.Name, category = status.Category.ToString() });
        var priorities = (await services.GetRequiredService<ICatalog<TicketPriority>>().GetAllAsync(cancellationToken))
            .OrderBy(priority => priority.Rank)
            .Select(priority => new { id = priority.ItemId, name = priority.Name });

        var categories = (await services.GetRequiredService<ITicketCategoryService>().ListAsync(TicketsConstants.DefaultContentType, cancellationToken: cancellationToken))
            .Select(category => new { id = category.Id, name = category.PathText });

        return Json(new { desks, statuses, priorities, categories });
    }
}

/// <summary>
/// Finds tickets the user may view.
/// </summary>
public sealed class SearchTicketsTool : TicketAITool
{
    public const string TheName = "searchTickets";

    public SearchTicketsTool()
        : base("""
            "query": { "type": "string", "description": "A ticket number or words of the subject." },
            "status": { "type": "string", "description": "Status categories, comma separated: New, Open, Pending, OnHold, Solved, Closed." },
            "deskId": { "type": "string", "description": "Only this desk's tickets." },
            "assignee": { "type": "string", "description": "'me', 'unassigned' or a user id." },
            "take": { "type": "integer", "description": "How many to return, at most 25. Defaults to 10." }
            """)
    {
    }

    public override string Name => TheName;

    public override string Description => "Finds the tickets the user may view, latest change first: by number or subject words, status category, desk and assignee. Returns each ticket's id, number, subject, status, priority, people and times.";

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var take = arguments.TryGetFirstString("take", out var takeText) && int.TryParse(takeText, out var number) ? Math.Clamp(number, 1, 25) : 10;
        var query = new TicketQuery
        {
            Search = Text(arguments, "query"),
            DeskIds = Text(arguments, "deskId") is { } deskId ? [deskId] : [],
            Categories = [.. (Text(arguments, "status") ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Enum.TryParse<TicketStatusCategory>(value, ignoreCase: true, out var category) ? category : (TicketStatusCategory?)null)
                .Where(category => category.HasValue)
                .Select(category => category.Value)],
            Assignee = Text(arguments, "assignee"),
            Take = take,
        };

        if (await arguments.Services.GetRequiredService<ITicketAccessService>().ApplyScopeAsync(user, query, cancellationToken) is not { } scoped)
        {
            return "The user may not view any tickets.";
        }

        var (tickets, total) = await arguments.Services.GetRequiredService<ITicketService>().ListAsync(scoped, cancellationToken);

        return Json(new { total, tickets = tickets.Select(TicketApiModel.From) });
    }
}

/// <summary>
/// Reads a ticket and its latest replies, notes and events.
/// </summary>
public sealed class GetTicketTool : TicketAITool
{
    public const string TheName = "getTicket";

    public GetTicketTool()
        : base("""
            "ticket": { "type": "string", "description": "The ticket's id or number, such as T-1042." }
            """, "ticket")
    {
    }

    public override string Name => TheName;

    public override string Description => "Reads a ticket the user may view: its values and its latest 20 replies, internal notes and events, newest first.";

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (await FindVisibleAsync(arguments, user, cancellationToken) is not { } ticket)
        {
            return "No ticket the user may view has that id or number.";
        }

        var page = await arguments.Services.GetRequiredService<ITimelineService>().ListAsync(new TimelineQuery
        {
            Subject = new RecordRef(TicketsConstants.RecordKind, ticket.ContentItemId),
            IncludeInternal = true,
            Take = 20,
        }, cancellationToken);

        return Json(new { ticket = TicketApiModel.From(ticket), entries = page.Entries.Select(entry => TicketApiEntry.From(entry, [], null)) });
    }
}

/// <summary>
/// Opens a ticket for a requester.
/// </summary>
public sealed class CreateTicketTool : TicketAITool
{
    public const string TheName = "createTicket";

    public CreateTicketTool()
        : base("""
            "subject": { "type": "string", "description": "A short summary of the problem." },
            "body": { "type": "string", "description": "The description, as the requester gave it." },
            "deskId": { "type": "string", "description": "The desk, from listTicketOptions." },
            "priorityId": { "type": "string", "description": "The priority, from listTicketOptions. The category's or the desk's default when left out." },
            "categoryId": { "type": "string", "description": "The category, from listTicketOptions, when tickets have categories." },
            "requesterName": { "type": "string", "description": "The requester's name." },
            "requesterEmail": { "type": "string", "description": "The requester's e-mail." }
            """, "subject", "body", "deskId")
    {
    }

    public override string Name => TheName;

    public override string Description => "Opens a ticket on a desk the user may reply in, for the requester named, and returns it with its number.";

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var services = arguments.Services;
        var desk = Text(arguments, "deskId") is { } deskId ? await services.GetRequiredService<ICatalog<TicketDesk>>().FindByIdAsync(deskId, cancellationToken) : null;

        if (desk is null)
        {
            return "Name a desk from listTicketOptions.";
        }

        if (!await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, TicketsPermissions.ForDesk(TicketOperation.Reply, desk)))
        {
            return "The user may not open tickets on that desk.";
        }

        var name = Text(arguments, "requesterName");
        var email = Text(arguments, "requesterEmail");
        var result = await services.GetRequiredService<ITicketService>().CreateAsync(new TicketCreateRequest
        {
            Ticket = await services.GetRequiredService<IContentManager>().NewAsync(TicketsConstants.DefaultContentType),
            ContentType = TicketsConstants.DefaultContentType,
            Subject = Text(arguments, "subject"),
            Body = Text(arguments, "body"),
            DeskId = desk.ItemId,
            PriorityId = Text(arguments, "priorityId"),
            CategoryId = Text(arguments, "categoryId"),
            RequesterContact = name is null && email is null ? null : new ContactInfo { Name = name, Email = email },
            Channel = TicketsConstants.Channels.Agent,
            CreatedBy = CallerOf(user),
        }, cancellationToken);

        return result.Succeeded
            ? Json(TicketApiModel.From(result.Ticket))
            : "The ticket wasn't opened: " + string.Join(" ", result.Errors);
    }
}

/// <summary>
/// Sends a public reply or adds an internal note.
/// </summary>
public sealed class AddTicketReplyTool : TicketAITool
{
    public const string TheName = "addTicketReply";

    public AddTicketReplyTool()
        : base("""
            "ticket": { "type": "string", "description": "The ticket's id or number." },
            "body": { "type": "string", "description": "The text." },
            "internal": { "type": "boolean", "description": "True for an internal note the requester never sees; false (the default) for a public reply sent to them." },
            "setStatusId": { "type": "string", "description": "A status to move the ticket to with a public reply, from listTicketOptions." }
            """, "ticket", "body")
    {
    }

    public override string Name => TheName;

    public override string Description => "Sends a public reply to the requester of a ticket, or adds an internal note, as the user. A public reply is e-mailed to the requester, so only send one the user asked for.";

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (await FindVisibleAsync(arguments, user, cancellationToken) is not { } ticket)
        {
            return "No ticket the user may view has that id or number.";
        }

        var isInternal = arguments.TryGetFirstString("internal", out var internalText) && bool.TryParse(internalText, out var flag) && flag;

        if (!await arguments.Services.GetRequiredService<ITicketAccessService>().CanAsync(user, ticket, isInternal ? TicketOperation.Note : TicketOperation.Reply, cancellationToken))
        {
            return isInternal ? "The user may not add notes to this ticket." : "The user may not reply on this ticket.";
        }

        if (!isInternal && Text(arguments, "setStatusId") is not null && !await arguments.Services.GetRequiredService<ITicketAccessService>().CanAsync(user, ticket, TicketOperation.Edit, cancellationToken))
        {
            return "The user may not change this ticket's status.";
        }

        var result = await arguments.Services.GetRequiredService<ITicketService>().ReplyAsync(ticket.ContentItemId, new TicketReply
        {
            Body = Text(arguments, "body"),
            IsInternal = isInternal,
            Author = CallerOf(user),
            AuthorName = user.Identity?.Name,
            Channel = TicketsConstants.Channels.Agent,
            SetStatusId = isInternal ? null : Text(arguments, "setStatusId"),
        }, cancellationToken);

        return result.Succeeded
            ? Json(TicketApiModel.From(result.Ticket ?? ticket))
            : "Nothing was added: " + string.Join(" ", result.Errors);
    }
}

/// <summary>
/// Changes a ticket's status, priority, team or assignee.
/// </summary>
public sealed class UpdateTicketTool : TicketAITool
{
    public const string TheName = "updateTicket";

    public UpdateTicketTool()
        : base("""
            "ticket": { "type": "string", "description": "The ticket's id or number." },
            "statusId": { "type": "string", "description": "The new status, from listTicketOptions." },
            "priorityId": { "type": "string", "description": "The new priority, from listTicketOptions." },
            "teamId": { "type": "string", "description": "The new team; an empty text clears it." },
            "assigneeId": { "type": "string", "description": "The user to assign it to; an empty text unassigns it." },
            "categoryId": { "type": "string", "description": "The new category, from listTicketOptions; an empty text clears it. Only the category changes." }
            """, "ticket")
    {
    }

    public override string Name => TheName;

    public override string Description => "Changes a ticket's status, priority or category (the user needs to be able to edit it) or its team or assignee (the user needs to be able to assign it). Values left out stay as they are.";

    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        if (await FindVisibleAsync(arguments, user, cancellationToken) is not { } ticket)
        {
            return "No ticket the user may view has that id or number.";
        }

        var access = arguments.Services.GetRequiredService<ITicketAccessService>();
        var update = new TicketUpdate
        {
            StatusId = Text(arguments, "statusId"),
            PriorityId = Text(arguments, "priorityId"),
            TeamId = arguments.TryGetFirstString("teamId", out var teamId) ? teamId?.Trim() ?? string.Empty : null,
            AssigneeId = arguments.TryGetFirstString("assigneeId", out var assigneeId) ? assigneeId?.Trim() ?? string.Empty : null,
            CategoryId = arguments.TryGetFirstString("categoryId", out var categoryId) ? categoryId?.Trim() ?? string.Empty : null,
        };

        if ((update.StatusId is not null || update.PriorityId is not null || update.CategoryId is not null) && !await access.CanAsync(user, ticket, TicketOperation.Edit, cancellationToken))
        {
            return "The user may not change this ticket's status, priority or category.";
        }

        if ((update.TeamId is not null || update.AssigneeId is not null) && !await access.CanAsync(user, ticket, TicketOperation.Assign, cancellationToken))
        {
            return "The user may not assign this ticket.";
        }

        var result = await arguments.Services.GetRequiredService<ITicketService>().UpdateAsync(
            ticket.ContentItemId,
            update,
            TicketActor.User(user.FindFirstValue(ClaimTypes.NameIdentifier), user.Identity?.Name),
            cancellationToken);

        return result.Succeeded
            ? Json(TicketApiModel.From(result.Ticket ?? ticket))
            : "Nothing changed: " + string.Join(" ", result.Errors);
    }
}
