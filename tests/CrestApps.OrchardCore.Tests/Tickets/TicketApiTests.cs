using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Attachments.Models;
using CrestApps.OrchardCore.Attachments.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Api;
using CrestApps.OrchardCore.Tickets.Core.Api;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Models;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The tickets API checks each call as the ticket pages do: a caller with no scope can't list, a ticket out of scope is
/// not found (its existence isn't given away), a ticket is opened only on a desk the caller may reply in and comes from
/// the Api channel with its files kept on the first entry, team and assignee changes need the assign permission, and a
/// note needs the note permission and never changes the status.
/// </summary>
public sealed class TicketApiTests
{
    private static readonly TicketDesk _support = new() { ItemId = "support", Name = "Support" };

    [Fact]
    public async Task ACallerWithoutScope_CantList_AndAListPassesTheFilters()
    {
        // Arrange
        var access = new Mock<ITicketAccessService>();
        var queries = new List<TicketQuery>();
        access.Setup(service => service.ApplyScopeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<TicketQuery>(), It.IsAny<CancellationToken>()))
            .Callback((ClaimsPrincipal _, TicketQuery query, CancellationToken _) => queries.Add(query))
            .ReturnsAsync((ClaimsPrincipal user, TicketQuery query, CancellationToken _) => user.FindFirstValue(ClaimTypes.NameIdentifier) == "ana" ? new TicketScopedQuery { Query = query, AllDeskIds = ["support"] } : null);
        var tickets = new Mock<ITicketService>();
        tickets.Setup(service => service.ListAsync(It.IsAny<TicketScopedQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(([Ticket("t1")], 7));

        // Act
        var stranger = await TicketApiEndpoints.ListAsync(null, null, null, null, null, null, null, null, Context(User("stranger")), access.Object, tickets.Object);
        var ana = await TicketApiEndpoints.ListAsync(" printer ", "support", "open,pending,nonsense", null, null, null, 5, 500, Context(User("ana")), access.Object, tickets.Object);

        // Assert
        Assert.Equal(403, Assert.IsType<ProblemHttpResult>(stranger.Result).StatusCode);
        var page = Assert.IsType<Ok<TicketApiPage>>(ana.Result).Value;
        Assert.Equal(7, page.Total);
        Assert.Equal(TicketApiPage.MaxTake, page.Take);
        Assert.Equal("t1", Assert.Single(page.Items).Id);
        var query = queries.Last();
        Assert.Equal("printer", query.Search);
        Assert.Equal([TicketStatusCategory.Open, TicketStatusCategory.Pending], query.Categories);
        Assert.Equal(5, query.Skip);
    }

    [Fact]
    public async Task ATicketOutOfScope_IsNotFound_AndATicketIsFoundByNumber()
    {
        // Arrange
        var ticket = Ticket("t1");
        var tickets = new Mock<ITicketService>();
        tickets.Setup(service => service.FindByNumberAsync("T-1")).ReturnsAsync(ticket);
        var access = Access(TicketOperation.ViewAssigned);

        // Act
        var found = await TicketApiEndpoints.GetAsync("T-1", Context(User("ana")), access.Object, tickets.Object);
        var hidden = await TicketApiEndpoints.GetAsync("T-1", Context(User("ana")), Access().Object, tickets.Object);

        // Assert
        Assert.Equal("t1", Assert.IsType<Ok<TicketApiModel>>(found.Result).Value.Id);
        Assert.Equal(404, Assert.IsType<ProblemHttpResult>(hidden.Result).StatusCode);
    }

    [Fact]
    public async Task ATicketIsOpenedOnlyOnADeskTheCallerMayReplyIn_FromTheApiChannel_WithItsFiles()
    {
        // Arrange
        TicketCreateRequest sent = null;
        var tickets = new Mock<ITicketService>();
        tickets.Setup(service => service.CreateAsync(It.IsAny<TicketCreateRequest>(), It.IsAny<CancellationToken>()))
            .Callback((TicketCreateRequest request, CancellationToken _) => sent = request)
            .ReturnsAsync((TicketCreateRequest request, CancellationToken _) => new TicketResult { Ticket = request.Ticket });
        var uploads = new List<AttachmentUpload>();
        var attachments = new Mock<IAttachmentService>();
        attachments.Setup(service => service.AddAsync(It.IsAny<IReadOnlyList<AttachmentUpload>>(), It.IsAny<CancellationToken>()))
            .Callback((IReadOnlyList<AttachmentUpload> files, CancellationToken _) => uploads.AddRange(files))
            .ReturnsAsync(new AttachmentResult { Attachments = [new Attachment { ItemId = "a1" }] });
        var services = new ServiceCollection().AddSingleton(attachments.Object).BuildServiceProvider();
        var request = new TicketApiCreateRequest
        {
            Subject = "Printer jams",
            Body = "It jams on every page.",
            DeskId = "support",
            RequesterEmail = "pat@example.com",
            Attachments = [new TicketApiFile { FileName = "jam.png", ContentType = "image/png", Content = [1, 2, 3] }],
        };
        var replier = User("ana", TicketsPermissions.ForDesk(TicketOperation.Reply, _support).Name);

        // Act
        var refused = await TicketApiEndpoints.CreateAsync(request, Context(User("ben")), new FakeAuthorization(), Desks(), Contents(), Definitions(), tickets.Object, services);
        var badType = await TicketApiEndpoints.CreateAsync(new TicketApiCreateRequest { DeskId = "support", Type = "Article" }, Context(replier), new FakeAuthorization(), Desks(), Contents(), Definitions(), tickets.Object, services);
        var created = await TicketApiEndpoints.CreateAsync(request, Context(replier), new FakeAuthorization(), Desks(), Contents(), Definitions(), tickets.Object, services);

        // Assert
        Assert.Equal(403, Assert.IsType<ProblemHttpResult>(refused.Result).StatusCode);
        Assert.IsType<ValidationProblem>(badType.Result);
        Assert.IsType<Created<TicketApiModel>>(created.Result);
        Assert.Equal(TicketsConstants.Channels.Api, sent.Channel);
        Assert.Equal(new PartyRef(PartyKinds.User, "ana"), sent.CreatedBy);
        Assert.Equal("pat@example.com", sent.RequesterContact.Email);
        Assert.Equal(["a1"], sent.AttachmentIds);
        Assert.Equal(new RecordRef(TimelineConstants.AttachmentOwnerKind, sent.EntryId), Assert.Single(uploads).Owner);
    }

    [Fact]
    public async Task AssigningNeedsTheAssignPermission()
    {
        // Arrange
        var tickets = new Mock<ITicketService>();
        tickets.Setup(service => service.GetAsync("t1")).ReturnsAsync(Ticket("t1"));
        tickets.Setup(service => service.UpdateAsync("t1", It.IsAny<TicketUpdate>(), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketResult { Ticket = Ticket("t1") });
        var editor = Access(TicketOperation.ViewAssigned, TicketOperation.Edit);

        // Act
        var assign = await TicketApiEndpoints.UpdateAsync("t1", new TicketApiUpdateRequest { AssigneeId = "ben" }, Context(User("ana")), editor.Object, Desks(), tickets.Object);
        var edit = await TicketApiEndpoints.UpdateAsync("t1", new TicketApiUpdateRequest { PriorityId = "high" }, Context(User("ana")), editor.Object, Desks(), tickets.Object);

        // Assert
        Assert.Equal(403, Assert.IsType<ProblemHttpResult>(assign.Result).StatusCode);
        Assert.IsType<Ok<TicketApiModel>>(edit.Result);
        tickets.Verify(service => service.UpdateAsync("t1", It.Is<TicketUpdate>(update => update.PriorityId == "high" && update.AssigneeId == null), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ANoteNeedsTheNotePermission_AndNeverChangesTheStatus()
    {
        // Arrange
        TicketReply sent = null;
        var tickets = new Mock<ITicketService>();
        tickets.Setup(service => service.GetAsync("t1")).ReturnsAsync(Ticket("t1"));
        tickets.Setup(service => service.ReplyAsync("t1", It.IsAny<TicketReply>(), It.IsAny<CancellationToken>()))
            .Callback((string _, TicketReply reply, CancellationToken _) => sent = reply)
            .ReturnsAsync(new TicketResult { Ticket = Ticket("t1") });
        var timeline = new Mock<ITimelineService>();
        timeline.Setup(service => service.FindAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => new TimelineEntry { ItemId = id, Kind = TicketsConstants.EntryKinds.Note, Visibility = TimelineVisibility.Internal, Body = "Checked the logs" });
        var services = new ServiceCollection().BuildServiceProvider();
        var request = new TicketApiReplyRequest { Body = "Checked the logs", SetStatusId = "solved" };

        // Act
        var refused = await TicketApiEndpoints.NoteAsync("t1", request, Context(User("ana")), Access(TicketOperation.ViewAssigned, TicketOperation.Reply).Object, tickets.Object, timeline.Object, services);
        var added = await TicketApiEndpoints.NoteAsync("t1", request, Context(User("ana")), Access(TicketOperation.ViewAssigned, TicketOperation.Note).Object, tickets.Object, timeline.Object, services);

        // Assert
        Assert.Equal(403, Assert.IsType<ProblemHttpResult>(refused.Result).StatusCode);
        Assert.True(Assert.IsType<Created<TicketApiEntry>>(added.Result).Value.IsInternal);
        Assert.True(sent.IsInternal);
        Assert.Null(sent.SetStatusId);
        Assert.Equal(TicketsConstants.Channels.Api, sent.Channel);
    }

    private static ContentItem Ticket(string id)
    {
        var item = new ContentItem { ContentItemId = id, ContentType = TicketsConstants.DefaultContentType, DisplayText = id };
        item.Apply(new TicketPart { Number = "T-1", DeskId = "support" });

        return item;
    }

    private static Mock<ITicketAccessService> Access(params TicketOperation[] allowed)
    {
        var access = new Mock<ITicketAccessService>();
        access.Setup(service => service.CanAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<ContentItem>(), It.IsAny<TicketOperation>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClaimsPrincipal _, ContentItem _, TicketOperation operation, CancellationToken _) => allowed.Contains(operation));
        access.Setup(service => service.GetViewScopesAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, TicketViewScope> { ["support"] = TicketViewScope.All });

        return access;
    }

    private static ICatalog<TicketDesk> Desks()
    {
        var desks = new Mock<ICatalog<TicketDesk>>();
        desks.Setup(catalog => catalog.FindByIdAsync("support", It.IsAny<CancellationToken>())).ReturnsAsync(_support);

        return desks.Object;
    }

    private static IContentManager Contents()
    {
        var contents = new Mock<IContentManager>();
        contents.Setup(manager => manager.NewAsync(It.IsAny<string>())).ReturnsAsync((string type) => new ContentItem { ContentType = type, ContentItemId = "new" });

        return contents.Object;
    }

    private static IContentDefinitionManager Definitions()
    {
        var ticket = new ContentTypeDefinition(
            TicketsConstants.DefaultContentType,
            TicketsConstants.DefaultContentType,
            [new ContentTypePartDefinition(TicketsConstants.PartName, new ContentPartDefinition(TicketsConstants.PartName, [], []), [])],
            []);
        var article = new ContentTypeDefinition("Article", "Article", [], []);
        var definitions = new Mock<IContentDefinitionManager>();
        definitions.Setup(manager => manager.GetTypeDefinitionAsync(It.IsAny<string>()))
            .ReturnsAsync((string name) => name == ticket.Name ? ticket : name == article.Name ? article : null);

        return definitions.Object;
    }

    private static ClaimsPrincipal User(string userId, params string[] permissions)
        => CalendarTestHarness.User(userId, permissions);

    private static DefaultHttpContext Context(ClaimsPrincipal user)
        => new() { User = user };

    private sealed class FakeAuthorization : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(requirements.All(requirement => requirement is PermissionRequirement permission && user.HasClaim(Permission.ClaimType, permission.Permission.Name))
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());
    }
}
