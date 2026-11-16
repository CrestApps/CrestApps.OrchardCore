using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tickets.Controllers;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using CrestApps.OrchardCore.Tickets.ViewModels;
using CrestApps.OrchardCore.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement.Display;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.DisplayManagement;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// The ticket pages' checks: sending a reply that sets a status needs the edit permission; a refused reply or note comes
/// back as it was written (an internal note stays internal); the properties panel applies only what the user changed and
/// refuses a value someone else changed meanwhile; a desk move needs the edit permission on the new desk; a bulk action
/// leaves out tickets the user can't see.
/// </summary>
public sealed class TicketsControllerTests
{
    private static readonly TicketDesk _support = new() { ItemId = "support", Name = "Support" };
    private static readonly TicketDesk _hr = new() { ItemId = "hr", Name = "HR" };

    [Fact]
    public async Task AReplyThatSetsAStatus_NeedsTheEditPermission_AndTheDraftStaysANote()
    {
        // Arrange
        var kit = new ControllerKit(TicketOperation.ViewAssigned, TicketOperation.Note);

        // Act
        var result = await kit.Controller.Reply("t1", "Checked the logs", isInternal: true, setStatusId: "solved", files: null);

        // Assert
        Assert.IsType<RedirectToActionResult>(result);
        kit.Tickets.Verify(service => service.ReplyAsync(It.IsAny<string>(), It.IsAny<TicketReply>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal("t1\nnote\n\nChecked the logs", kit.Controller.TempData.Values.Single());
    }

    [Fact]
    public async Task ARefusedNote_ComesBackAsANote_WithItsStatus()
    {
        // Arrange
        var kit = new ControllerKit(TicketOperation.ViewAssigned, TicketOperation.Note, TicketOperation.Edit);
        kit.Tickets.Setup(service => service.ReplyAsync("t1", It.IsAny<TicketReply>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketResult { Errors = ["The message is too long."] });

        // Act
        await kit.Controller.Reply("t1", "Internal only", isInternal: true, setStatusId: "pending", files: null);

        // Assert
        Assert.Equal("t1\nnote\npending\nInternal only", kit.Controller.TempData.Values.Single());
    }

    [Fact]
    public async Task ThePanel_AppliesOnlyWhatTheUserChanged_AndRefusesAValueChangedMeanwhile()
    {
        // Arrange
        var kit = new ControllerKit(TicketOperation.ViewAssigned, TicketOperation.Edit, TicketOperation.Assign);
        kit.Tickets.Setup(service => service.UpdateAsync("t1", It.IsAny<TicketUpdate>(), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketResult { Ticket = kit.Ticket });

        // The page showed status "new" and assignee "ana"; meanwhile someone moved it to "open" and gave it to "ben".
        var loaded = new TicketUpdate { StatusId = "new", PriorityId = "normal", DeskId = "support", AssigneeId = "ana" };

        // Act: one agent changes only the priority; another changes the status they last saw as "new".
        await kit.Controller.Update("t1", new TicketUpdate { StatusId = "new", PriorityId = "urgent", DeskId = "support", AssigneeId = "ana" }, loaded);
        await kit.Controller.Update("t1", new TicketUpdate { StatusId = "solved", PriorityId = "normal", DeskId = "support", AssigneeId = "ana" }, loaded);

        // Assert
        kit.Tickets.Verify(service => service.UpdateAsync("t1", It.Is<TicketUpdate>(update => update.PriorityId == "urgent" && update.StatusId == null && update.AssigneeId == null), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()), Times.Once);
        kit.Tickets.Verify(service => service.UpdateAsync("t1", It.Is<TicketUpdate>(update => update.StatusId == "solved"), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ADeskMove_NeedsTheEditPermissionOnTheNewDesk()
    {
        // Arrange
        var kit = new ControllerKit(TicketOperation.ViewAssigned, TicketOperation.Edit);
        kit.Grant(TicketsPermissions.ForDesk(TicketOperation.ViewAll, _hr));
        var loaded = new TicketUpdate { StatusId = "open", PriorityId = "normal", DeskId = "support", AssigneeId = "ben" };

        // Act
        await kit.Controller.Update("t1", new TicketUpdate { StatusId = "open", PriorityId = "normal", DeskId = "hr", AssigneeId = "ben" }, loaded);

        // Assert
        kit.Tickets.Verify(service => service.UpdateAsync(It.IsAny<string>(), It.IsAny<TicketUpdate>(), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ABulkAction_LeavesOutTicketsTheUserCantSee()
    {
        // Arrange
        var kit = new ControllerKit(TicketOperation.ViewAssigned, TicketOperation.Assign);
        var hidden = Ticket("t2", "hr");
        kit.Tickets.Setup(service => service.GetAsync("t2")).ReturnsAsync(hidden);
        kit.Access.Setup(service => service.CanAsync(It.IsAny<ClaimsPrincipal>(), hidden, It.IsAny<TicketOperation>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        kit.Tickets.Setup(service => service.UpdateAsync(It.IsAny<string>(), It.IsAny<TicketUpdate>(), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketResult { Ticket = kit.Ticket });

        // Act
        await kit.Controller.Bulk(TicketsController.BulkActions.AssignToMe, ["t1", "t2"], null);

        // Assert
        kit.Tickets.Verify(service => service.UpdateAsync("t2", It.IsAny<TicketUpdate>(), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()), Times.Never);
        kit.Tickets.Verify(service => service.UpdateAsync("t1", It.IsAny<TicketUpdate>(), It.IsAny<TicketActor>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task NewTicket_WithoutAType_AsksForOne_OfTheTypesADeskTheAgentCreatesOnTakes()
    {
        // Arrange: Ben replies on Support, which takes tickets and IT requests; HR takes only HR requests, and Ben may not
        // reply there.
        var support = new TicketDesk { ItemId = "support", Name = "Support", TicketTypes = ["Ticket", "ITRequest"] };
        var hr = new TicketDesk { ItemId = "hr", Name = "HR", TicketTypes = ["HRRequest"] };
        var kit = new ControllerKit(["Ticket", "ITRequest", "HRRequest"], [support, hr]);
        kit.Grant(TicketsPermissions.ForDesk(TicketOperation.Reply, support));

        // Act
        var result = await kit.Controller.Create(accountId: "acme", requester: "User:dana");

        // Assert: the type is chosen once, before the form, and the account and requester come along.
        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("ChooseType", view.ViewName);
        var model = Assert.IsType<TicketTypeChoiceViewModel>(view.Model);
        Assert.Equal(["IT request", "Ticket"], model.Types.Select(type => type.Text));
        Assert.Equal(("acme", "User:dana"), (model.AccountId, model.Requester));
    }

    [Fact]
    public async Task NewTicket_WithOneTypeToChooseFrom_OpensItsFormAtOnce()
    {
        // Arrange
        var support = new TicketDesk { ItemId = "support", Name = "Support", TicketTypes = ["Ticket"] };
        var hr = new TicketDesk { ItemId = "hr", Name = "HR", TicketTypes = ["HRRequest"] };
        var kit = new ControllerKit(["Ticket", "HRRequest"], [support, hr]);
        kit.Grant(TicketsPermissions.ForDesk(TicketOperation.Reply, support));

        // Act
        var result = await kit.Controller.Create();

        // Assert
        var view = Assert.IsType<ViewResult>(result);
        Assert.Null(view.ViewName);
        Assert.Equal("Ticket", Assert.IsType<TicketCreateViewModel>(view.Model).ContentType);
    }

    private static ContentItem Ticket(string id, string deskId, string statusId = "open", string assigneeId = "ben")
    {
        var item = new ContentItem { ContentItemId = id, ContentType = TicketsConstants.DefaultContentType, DisplayText = id };
        item.Apply(new TicketPart { Number = id, DeskId = deskId, StatusId = statusId, StatusCategory = TicketStatusCategory.Open, PriorityId = "normal", AssigneeId = assigneeId });

        return item;
    }

    private sealed class ControllerKit
    {
        private readonly HashSet<string> _granted = new(StringComparer.Ordinal);

        public ControllerKit(params TicketOperation[] allowed)
            : this(null, null, allowed)
        {
        }

        public ControllerKit(string[] types, TicketDesk[] desks, params TicketOperation[] allowed)
        {
            desks ??= [_support, _hr];

            Ticket = TicketsControllerTests.Ticket("t1", "support");
            Tickets.Setup(service => service.GetAsync("t1")).ReturnsAsync(Ticket);
            Access.Setup(service => service.CanAsync(It.IsAny<ClaimsPrincipal>(), Ticket, It.IsAny<TicketOperation>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ClaimsPrincipal _, ContentItem _, TicketOperation operation, CancellationToken _) => allowed.Contains(operation));
            Access.Setup(service => service.GetViewScopesAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, TicketViewScope> { ["support"] = TicketViewScope.All, ["hr"] = TicketViewScope.All });

            var deskCatalog = new Mock<ICatalog<TicketDesk>>();
            deskCatalog.Setup(catalog => catalog.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(desks);
            deskCatalog.Setup(catalog => catalog.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => desks.FirstOrDefault(desk => desk.ItemId == id));

            var authorization = new Mock<IAuthorizationService>();
            authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
                .ReturnsAsync((ClaimsPrincipal _, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
                    requirements.OfType<PermissionRequirement>().All(requirement => _granted.Contains(requirement.Permission.Name)) ? AuthorizationResult.Success() : AuthorizationResult.Failed());

            var htmlLocalizer = new Mock<IHtmlLocalizer<TicketsController>>();
            htmlLocalizer.Setup(value => value[It.IsAny<string>()]).Returns((string name) => new LocalizedHtmlString(name, name));
            htmlLocalizer.Setup(value => value[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string name, object[] _) => new LocalizedHtmlString(name, name));

            var services = new ServiceCollection()
                .AddSingleton(new global::CrestApps.OrchardCore.Tickets.Services.TicketUploads([], Microsoft.Extensions.Options.Options.Create(new global::CrestApps.OrchardCore.Attachments.Models.AttachmentOptions()), Doubles.RecipeRoundTrip.Localizer<global::CrestApps.OrchardCore.Tickets.Services.TicketUploads>()))
                .BuildServiceProvider();
            var httpContext = new DefaultHttpContext { User = CalendarTestHarness.User("ben"), RequestServices = services };

            Controller = new TicketsController(
                Tickets.Object,
                Access.Object,
                new Mock<ICatalog<TicketStatus>>().Object,
                Priorities().Object,
                deskCatalog.Object,
                new Mock<ICatalog<TicketView>>().Object,
                new Mock<IContentManager>().Object,
                Definitions(types).Object,
                new Mock<IContentItemDisplayManager>().Object,
                new Mock<IUpdateModelAccessor>().Object,
                new Mock<IPartyDirectory>().Object,
                new Mock<IShapeFactory>().Object,
                new Mock<INotifier>().Object,
                new Mock<IClock>().Object,
                authorization.Object,
                services,
                Options.Create(new PagerOptions()),
                htmlLocalizer.Object,
                RecipeRoundTrip.Localizer<TicketsController>())
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(httpContext, new Mock<ITempDataProvider>().Object),
                Url = new Mock<Microsoft.AspNetCore.Mvc.IUrlHelper>().Object,
            };
        }

        public ContentItem Ticket { get; }

        public Mock<ITicketService> Tickets { get; } = new();

        public Mock<ITicketAccessService> Access { get; } = new();

        public TicketsController Controller { get; }

        public void Grant(Permission permission)
            => _granted.Add(permission.Name);

        private static Mock<ICatalog<TicketPriority>> Priorities()
        {
            var priorities = new Mock<ICatalog<TicketPriority>>();
            priorities.Setup(catalog => catalog.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

            return priorities;
        }

        // The ticket types (content types with the Ticket part), named after their technical names.
        private static Mock<IContentDefinitionManager> Definitions(string[] types)
        {
            var definitions = new Mock<IContentDefinitionManager>();
            var names = new Dictionary<string, string>(StringComparer.Ordinal) { ["Ticket"] = "Ticket", ["ITRequest"] = "IT request", ["HRRequest"] = "HR request" };
            var list = (types ?? []).Select(name => new ContentTypeDefinition(name, names.GetValueOrDefault(name, name),
                [new ContentTypePartDefinition(TicketsConstants.PartName, new ContentPartDefinition(TicketsConstants.PartName, [], []), [])], [])).ToList();
            definitions.Setup(value => value.ListTypeDefinitionsAsync()).ReturnsAsync(list);
            definitions.Setup(value => value.GetTypeDefinitionAsync(It.IsAny<string>())).ReturnsAsync((string name) => list.FirstOrDefault(type => type.Name == name));

            return definitions;
        }
    }
}
