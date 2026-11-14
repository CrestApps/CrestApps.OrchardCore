using CrestApps.Core;
using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Api;
using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets.Core.Api;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;

namespace CrestApps.OrchardCore.Tickets.Api;

/// <summary>
/// The tickets API, <c>api/tickets/v1</c>: list, get, open, change, reply to and add notes to tickets, with files. Every
/// call is checked against the caller's ticket permissions and scope, as the ticket pages are; writes take an
/// <c>Idempotency-Key</c> so they are safe to send again.
/// </summary>
internal static class TicketApiEndpoints
{
    public const string Prefix = "api/tickets/v1";

    public const string Tag = "Tickets";

    // How long a file's download address in an entry works.
    private static readonly TimeSpan _linkLifetime = TimeSpan.FromHours(1);

    public static IEndpointRouteBuilder MapTicketsApi(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapModuleApi(Prefix, Tag);

        group.MapGet("tickets", ListAsync)
            .WithName("ListTickets")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Lists the tickets the caller may view, newest change first: searched by number or subject, and filtered by desk, status category, assignee, requester or account.");

        group.MapGet("tickets/{ticketId}", GetAsync)
            .WithName("GetTicket")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Gets a ticket by its id or number.");

        group.MapGet("tickets/{ticketId}/entries", EntriesAsync)
            .WithName("ListTicketEntries")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Lists a ticket's replies, internal notes and events, newest first, with download addresses for their files.");

        group.MapPost("tickets", CreateAsync)
            .WithName("CreateTicket")
            .WithIdempotency()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesValidationProblem()
            .WithSummary("Opens a ticket on a desk the caller may reply in.");

        group.MapPatch("tickets/{ticketId}", UpdateAsync)
            .WithName("UpdateTicket")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .WithSummary("Changes a ticket's subject, status, priority or desk (edit permission), or its team and assignee (assign permission).");

        group.MapPost("tickets/{ticketId}/replies", ReplyAsync)
            .WithName("ReplyToTicket")
            .WithIdempotency()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .WithSummary("Sends a public reply on a ticket, with files, and can move the ticket to another status.");

        group.MapPost("tickets/{ticketId}/notes", NoteAsync)
            .WithName("AddTicketNote")
            .WithIdempotency()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem()
            .WithSummary("Adds an internal note to a ticket, with files.");

        return routes;
    }

    internal static async Task<Results<Ok<TicketApiPage>, ProblemHttpResult>> ListAsync(
        string q,
        string deskId,
        string status,
        string assignee,
        string requester,
        string accountId,
        int? skip,
        int? take,
        HttpContext httpContext,
        ITicketAccessService access,
        ITicketService tickets)
    {
        var pageSkip = TicketApiPage.ClampSkip(skip);
        var pageTake = TicketApiPage.ClampTake(take);
        var query = new TicketQuery
        {
            Search = string.IsNullOrWhiteSpace(q) ? null : q.Trim(),
            DeskIds = string.IsNullOrEmpty(deskId) ? [] : [deskId],
            Categories = [.. (status ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => Enum.TryParse<TicketStatusCategory>(value, ignoreCase: true, out var category) ? category : (TicketStatusCategory?)null)
                .Where(category => category.HasValue)
                .Select(category => category.Value)],
            Assignee = string.IsNullOrEmpty(assignee) ? null : assignee,
            Requester = string.IsNullOrEmpty(requester) ? null : requester,
            AccountId = string.IsNullOrEmpty(accountId) ? null : accountId,
            Skip = pageSkip,
            Take = pageTake,
        };

        if (await access.ApplyScopeAsync(httpContext.User, query, httpContext.RequestAborted) is not { } scoped)
        {
            return ApiProblems.Forbidden();
        }

        var (items, total) = await tickets.ListAsync(scoped, httpContext.RequestAborted);

        return TypedResults.Ok(new TicketApiPage([.. items.Select(TicketApiModel.From)], total, pageSkip, pageTake));
    }

    internal static async Task<Results<Ok<TicketApiModel>, ProblemHttpResult>> GetAsync(
        string ticketId,
        HttpContext httpContext,
        ITicketAccessService access,
        ITicketService tickets)
    {
        if (await FindAsync(tickets, ticketId) is not { } ticket)
        {
            return ApiProblems.NotFound();
        }

        return await access.CanAsync(httpContext.User, ticket, TicketOperation.ViewAssigned, httpContext.RequestAborted)
            ? TypedResults.Ok(TicketApiModel.From(ticket))
            : ApiProblems.NotFound();
    }

    internal static async Task<Results<Ok<IReadOnlyList<TicketApiEntry>>, ProblemHttpResult>> EntriesAsync(
        string ticketId,
        int? skip,
        int? take,
        HttpContext httpContext,
        ITicketAccessService access,
        ITicketService tickets,
        ITimelineService timeline,
        IServiceProvider services)
    {
        if (await FindAsync(tickets, ticketId) is not { } ticket
            || !await access.CanAsync(httpContext.User, ticket, TicketOperation.ViewAssigned, httpContext.RequestAborted))
        {
            return ApiProblems.NotFound();
        }

        var page = await timeline.ListAsync(new TimelineQuery
        {
            Subject = new RecordRef(TicketsConstants.RecordKind, ticket.ContentItemId),
            IncludeInternal = true,
            Skip = TicketApiPage.ClampSkip(skip),
            Take = TicketApiPage.ClampTake(take),
        }, httpContext.RequestAborted);

        var attachments = services.GetService<IAttachmentService>();
        var files = attachments is null
            ? []
            : await attachments.GetAsync(page.Entries.SelectMany(entry => entry.AttachmentIds ?? []), httpContext.RequestAborted);
        var byEntry = files.ToLookup(file => file.Owner.Id, StringComparer.Ordinal);
        var basePath = httpContext.Request.PathBase.Value ?? string.Empty;

        IReadOnlyList<TicketApiEntry> entries = [.. page.Entries.Select(entry => TicketApiEntry.From(
            entry,
            byEntry[entry.ItemId],
            file => attachments is null ? null : basePath + attachments.CreateSignedPath(file.ItemId, _linkLifetime)))];

        return TypedResults.Ok(entries);
    }

    internal static async Task<Results<Created<TicketApiModel>, ProblemHttpResult, ValidationProblem>> CreateAsync(
        TicketApiCreateRequest request,
        HttpContext httpContext,
        IAuthorizationService authorizationService,
        ICatalog<TicketDesk> desks,
        IContentManager contentManager,
        IContentDefinitionManager contentDefinitionManager,
        ITicketService tickets,
        IServiceProvider services)
    {
        var cancellationToken = httpContext.RequestAborted;

        if (request is null)
        {
            return ApiProblems.Validation("body", "Send the ticket as JSON.");
        }

        var desk = string.IsNullOrEmpty(request.DeskId) ? null : await desks.FindByIdAsync(request.DeskId, cancellationToken);

        if (desk is null)
        {
            return ApiProblems.Validation(nameof(request.DeskId), "Name a desk.");
        }

        if (!await authorizationService.AuthorizeAsync(httpContext.User, TicketsPermissions.ForDesk(TicketOperation.Reply, desk)))
        {
            return ApiProblems.Forbidden();
        }

        PartyRef requester = default;

        if (!string.IsNullOrEmpty(request.Requester) && !PartyRef.TryParse(request.Requester, out requester))
        {
            return ApiProblems.Validation(nameof(request.Requester), "Give the requester as Kind:Id, such as User:abc or Contact:abc.");
        }

        var type = string.IsNullOrEmpty(request.Type) ? TicketsConstants.DefaultContentType : request.Type;

        if (await contentDefinitionManager.GetTypeDefinitionAsync(type) is not { } definition
            || !definition.Parts.Any(part => part.PartDefinition.Name == TicketsConstants.PartName))
        {
            return ApiProblems.Validation(nameof(request.Type), "There is no such ticket type.");
        }

        var ticket = await contentManager.NewAsync(type);

        var entryId = UniqueId.GenerateId();
        var (attached, uploadErrors) = await StoreFilesAsync(services, httpContext.User, request.Attachments, entryId, cancellationToken);

        if (uploadErrors is not null)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]> { [nameof(request.Attachments)] = uploadErrors });
        }

        var result = await tickets.CreateAsync(new TicketCreateRequest
        {
            Ticket = ticket,
            ContentType = type,
            Subject = request.Subject,
            Body = request.Body,
            EntryId = entryId,
            AttachmentIds = attached,
            DeskId = desk.ItemId,
            PriorityId = request.PriorityId,
            Requester = requester,
            RequesterContact = requester.IsEmpty && (!string.IsNullOrWhiteSpace(request.RequesterName) || !string.IsNullOrWhiteSpace(request.RequesterEmail))
                ? new ContactInfo { Name = request.RequesterName?.Trim(), Email = request.RequesterEmail?.Trim() }
                : null,
            AccountId = request.AccountId,
            Channel = TicketsConstants.Channels.Api,
            CreatedBy = CallerOf(httpContext.User),
        }, cancellationToken);

        if (!result.Succeeded)
        {
            await DeleteFilesAsync(services, attached);

            return ApiProblems.Validation(new Dictionary<string, string[]> { [string.Empty] = [.. result.Errors] });
        }

        return TypedResults.Created($"{httpContext.Request.PathBase}/{Prefix}/tickets/{result.Ticket.ContentItemId}", TicketApiModel.From(result.Ticket));
    }

    internal static async Task<Results<Ok<TicketApiModel>, ProblemHttpResult, ValidationProblem>> UpdateAsync(
        string ticketId,
        TicketApiUpdateRequest request,
        HttpContext httpContext,
        ITicketAccessService access,
        ICatalog<TicketDesk> desks,
        ITicketService tickets)
    {
        var cancellationToken = httpContext.RequestAborted;

        if (request is null)
        {
            return ApiProblems.Validation("body", "Send the changes as JSON.");
        }

        if (await FindAsync(tickets, ticketId) is not { } ticket
            || !await access.CanAsync(httpContext.User, ticket, TicketOperation.ViewAssigned, cancellationToken))
        {
            return ApiProblems.NotFound();
        }

        var editing = request.Subject is not null || request.StatusId is not null || request.PriorityId is not null || request.DeskId is not null || request.EscalationLevel is not null;
        var assigning = request.TeamId is not null || request.AssigneeId is not null;

        if ((editing && !await access.CanAsync(httpContext.User, ticket, TicketOperation.Edit, cancellationToken))
            || (assigning && !await access.CanAsync(httpContext.User, ticket, TicketOperation.Assign, cancellationToken)))
        {
            return ApiProblems.Forbidden();
        }

        if (!string.IsNullOrEmpty(request.DeskId) && await desks.FindByIdAsync(request.DeskId, cancellationToken) is { } newDesk
            && !(await access.GetViewScopesAsync(httpContext.User, cancellationToken)).ContainsKey(newDesk.ItemId))
        {
            return ApiProblems.Forbidden("You may not move tickets to that desk.");
        }

        var result = await tickets.UpdateAsync(ticket.ContentItemId, new TicketUpdate
        {
            Subject = request.Subject,
            StatusId = request.StatusId,
            PriorityId = request.PriorityId,
            DeskId = request.DeskId,
            TeamId = request.TeamId,
            AssigneeId = request.AssigneeId,
            EscalationLevel = request.EscalationLevel,
        }, ActorOf(httpContext.User), cancellationToken);

        if (!result.Succeeded)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]> { [string.Empty] = [.. result.Errors] });
        }

        return TypedResults.Ok(TicketApiModel.From(result.Ticket ?? await tickets.GetAsync(ticket.ContentItemId)));
    }

    internal static Task<Results<Created<TicketApiEntry>, ProblemHttpResult, ValidationProblem>> ReplyAsync(
        string ticketId,
        TicketApiReplyRequest request,
        HttpContext httpContext,
        ITicketAccessService access,
        ITicketService tickets,
        ITimelineService timeline,
        IServiceProvider services)
        => AddEntryAsync(ticketId, request, isInternal: false, httpContext, access, tickets, timeline, services);

    internal static Task<Results<Created<TicketApiEntry>, ProblemHttpResult, ValidationProblem>> NoteAsync(
        string ticketId,
        TicketApiReplyRequest request,
        HttpContext httpContext,
        ITicketAccessService access,
        ITicketService tickets,
        ITimelineService timeline,
        IServiceProvider services)
        => AddEntryAsync(ticketId, request, isInternal: true, httpContext, access, tickets, timeline, services);

    private static async Task<Results<Created<TicketApiEntry>, ProblemHttpResult, ValidationProblem>> AddEntryAsync(
        string ticketId,
        TicketApiReplyRequest request,
        bool isInternal,
        HttpContext httpContext,
        ITicketAccessService access,
        ITicketService tickets,
        ITimelineService timeline,
        IServiceProvider services)
    {
        var cancellationToken = httpContext.RequestAborted;

        if (request is null || string.IsNullOrWhiteSpace(request.Body))
        {
            return ApiProblems.Validation(nameof(TicketApiReplyRequest.Body), "Write the text.");
        }

        if (await FindAsync(tickets, ticketId) is not { } ticket
            || !await access.CanAsync(httpContext.User, ticket, TicketOperation.ViewAssigned, cancellationToken))
        {
            return ApiProblems.NotFound();
        }

        if (!await access.CanAsync(httpContext.User, ticket, isInternal ? TicketOperation.Note : TicketOperation.Reply, cancellationToken))
        {
            return ApiProblems.Forbidden();
        }

        var entryId = UniqueId.GenerateId();
        var (attached, uploadErrors) = await StoreFilesAsync(services, httpContext.User, request.Attachments, entryId, cancellationToken);

        if (uploadErrors is not null)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]> { [nameof(request.Attachments)] = uploadErrors });
        }

        var result = await tickets.ReplyAsync(ticket.ContentItemId, new TicketReply
        {
            Body = request.Body,
            IsInternal = isInternal,
            Author = CallerOf(httpContext.User),
            AuthorName = httpContext.User.Identity?.Name,
            Channel = TicketsConstants.Channels.Api,
            EntryId = entryId,
            AttachmentIds = attached,
            SetStatusId = isInternal || string.IsNullOrEmpty(request.SetStatusId) ? null : request.SetStatusId,
        }, cancellationToken);

        if (!result.Succeeded)
        {
            await DeleteFilesAsync(services, attached);

            return ApiProblems.Validation(new Dictionary<string, string[]> { [string.Empty] = [.. result.Errors] });
        }

        var entry = await timeline.FindAsync(entryId, cancellationToken);
        var attachments = services.GetService<IAttachmentService>();
        var files = attachments is null || attached.Count == 0 ? [] : await attachments.GetAsync(attached, cancellationToken);
        var basePath = httpContext.Request.PathBase.Value ?? string.Empty;

        return TypedResults.Created(
            $"{basePath}/{Prefix}/tickets/{ticket.ContentItemId}/entries",
            entry is null ? null : TicketApiEntry.From(entry, files, file => basePath + attachments.CreateSignedPath(file.ItemId, _linkLifetime)));
    }

    private static async Task<ContentItem> FindAsync(ITicketService tickets, string ticketId)
        => string.IsNullOrWhiteSpace(ticketId)
            ? null
            : await tickets.GetAsync(ticketId) ?? await tickets.FindByNumberAsync(ticketId);

    // Stores the sent files as files of the entry about to be added; the second value holds the reasons when a file is
    // refused.
    private static async Task<(List<string> Attached, string[] Errors)> StoreFilesAsync(IServiceProvider services, ClaimsPrincipal user, List<TicketApiFile> files, string entryId, CancellationToken cancellationToken)
    {
        var uploads = files?.Where(file => file?.Content is { Length: > 0 }).ToList() ?? [];

        if (uploads.Count == 0)
        {
            return ([], null);
        }

        if (services.GetService<IAttachmentService>() is not { } attachments)
        {
            return ([], ["Files can't be attached on this site."]);
        }

        var owner = new RecordRef(TimelineConstants.AttachmentOwnerKind, entryId);
        var stored = await attachments.AddAsync([.. uploads.Select(file => new AttachmentUpload
        {
            Owner = owner,
            FileName = file.FileName,
            DeclaredContentType = file.ContentType,
            Content = file.Content,
            UploadedBy = CallerOf(user),
        })], cancellationToken);

        return stored.Succeeded
            ? ([.. stored.Attachments.Select(attachment => attachment.ItemId)], null)
            : ([], [.. stored.Errors]);
    }

    private static async Task DeleteFilesAsync(IServiceProvider services, List<string> attachmentIds)
    {
        if (attachmentIds.Count == 0 || services.GetService<IAttachmentService>() is not { } attachments)
        {
            return;
        }

        foreach (var attachmentId in attachmentIds)
        {
            await attachments.DeleteAsync(attachmentId, CancellationToken.None);
        }
    }

    private static PartyRef CallerOf(ClaimsPrincipal user)
        => user?.FindFirstValue(ClaimTypes.NameIdentifier) is { Length: > 0 } userId ? new PartyRef(PartyKinds.User, userId) : default;

    private static TicketActor ActorOf(ClaimsPrincipal user)
        => TicketActor.User(user?.FindFirstValue(ClaimTypes.NameIdentifier), user?.Identity?.Name);
}
