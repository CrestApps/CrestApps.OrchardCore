using System.Runtime.CompilerServices;
using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Controllers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Filters;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Telephony;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Implementation;
using OrchardCore.DisplayManagement.Layout;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Zones;
using OrchardCore.ResourceManagement;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

// Every admin page but the workspace and the soft phone carries the messaging notifications: a toast when a customer
// writes or a conversation is handed over, and the count on Messaging > Inbox. Before this only the workspace listened,
// so an agent on any other admin page heard nothing. They are only added where a notification could reach the user.
public sealed class MessagingNotificationsFilterTests
{
    private const string UserId = "user-1";

    [Fact]
    public async Task OnResultExecutionAsync_OnAnAdminPage_ForAnAgent_AddsTheNotificationsForThatAgent()
    {
        var outcome = await RunAsync(new Request
        {
            Agent = new AgentProfile { ItemId = "agent-1", UserId = UserId },
            Granted = [MessagingPermissions.UseMessagingWorkspace],
        });

        Assert.NotNull(outcome.Shape);
        Assert.Equal("agent-1", outcome.Shape.Properties["AgentId"]);
        Assert.Equal(ResourceLocation.Foot, outcome.Script.Location);
        Assert.Equal(1, outcome.NextCalls);
    }

    // A supervisor without an agent profile still hears about the conversations nobody owns, so their pages carry the
    // notifications, for no agent.
    [Fact]
    public async Task OnResultExecutionAsync_ForASupervisorWithoutAProfile_AddsTheNotificationsWithoutAnAgent()
    {
        var outcome = await RunAsync(new Request
        {
            Granted = [MessagingPermissions.UseMessagingWorkspace, MessagingPermissions.ViewAllConversations],
        });

        Assert.NotNull(outcome.Shape);
        Assert.Null(outcome.Shape.Properties["AgentId"]);
        Assert.Equal(1, outcome.NextCalls);
    }

    [Fact]
    public async Task OnResultExecutionAsync_OutsideTheAdmin_AddsNothing()
    {
        var outcome = await RunAsync(AnAgent() with { Path = "/contact-us" });

        AssertNothingAdded(outcome);
    }

    // The workspace keeps its own connection and toasts; a second, passive one beside it would raise every toast twice.
    [Fact]
    public async Task OnResultExecutionAsync_OnTheWorkspace_AddsNothing()
    {
        var outcome = await RunAsync(AnAgent() with { Controller = RuntimeHelpers.GetUninitializedObject(typeof(AdminController)) });

        AssertNothingAdded(outcome);
    }

    // The soft phone page is the phone itself: a toast would cover the call controls, and a click on it would navigate
    // the phone window away from a live call.
    [Fact]
    public async Task OnResultExecutionAsync_OnTheSoftPhonePage_AddsNothing()
    {
        var outcome = await RunAsync(AnAgent() with { RouteName = TelephonyConstants.RouteNames.SoftPhonePage });

        AssertNothingAdded(outcome);
    }

    // Only a full page has a layout to add to.
    [Fact]
    public async Task OnResultExecutionAsync_ForAJsonResult_AddsNothing()
    {
        var outcome = await RunAsync(AnAgent() with { Result = new JsonResult(new { count = 1 }) });

        AssertNothingAdded(outcome);
    }

    [Fact]
    public async Task OnResultExecutionAsync_ForAPartialView_AddsNothing()
    {
        var outcome = await RunAsync(AnAgent() with { Result = new PartialViewResult() });

        AssertNothingAdded(outcome);
    }

    [Fact]
    public async Task OnResultExecutionAsync_WithoutTheWorkspacePermission_AddsNothing()
    {
        var outcome = await RunAsync(AnAgent() with { Granted = [] });

        AssertNothingAdded(outcome);
    }

    // Without a profile the hub puts the connection in no agent or queue group, and without the supervisor permission
    // not in the triage group either, so nothing could reach the page.
    [Fact]
    public async Task OnResultExecutionAsync_WithoutAProfileOrViewAll_AddsNothing()
    {
        var outcome = await RunAsync(new Request { Granted = [MessagingPermissions.UseMessagingWorkspace] });

        AssertNothingAdded(outcome);
    }

    private static Request AnAgent()
        => new()
        {
            Agent = new AgentProfile { ItemId = "agent-1", UserId = UserId },
            Granted = [MessagingPermissions.UseMessagingWorkspace],
        };

    private static void AssertNothingAdded(Outcome outcome)
    {
        Assert.Null(outcome.Shape);
        outcome.ResourceManager.Verify(manager => manager.RegisterResource(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        Assert.Equal(1, outcome.NextCalls);
    }

    private static async Task<Outcome> RunAsync(Request request)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId)], "Test")),
        };
        httpContext.Request.Path = request.Path;

        var actionDescriptor = new ActionDescriptor
        {
            AttributeRouteInfo = new AttributeRouteInfo { Name = request.RouteName },
        };

        var context = new ResultExecutingContext(
            new ActionContext(httpContext, new RouteData(), actionDescriptor),
            [],
            request.Result ?? new ViewResult(),
            request.Controller ?? new object());

        var layout = new ZoneHolding(() => ValueTask.FromResult<IShape>(new Shape()));
        var layoutAccessor = new Mock<ILayoutAccessor>();
        layoutAccessor.Setup(accessor => accessor.GetLayoutAsync()).ReturnsAsync(layout);

        var shapeFactory = new Mock<IShapeFactory>();
        shapeFactory
            .Setup(factory => factory.CreateAsync(
                MessagingNotificationsFilter.ShapeType,
                It.IsAny<Func<object, ValueTask<IShape>>>(),
                It.IsAny<Action<ShapeCreatingContext, object>>(),
                It.IsAny<Action<ShapeCreatedContext, object>>(),
                It.IsAny<object>()))
            .Returns(() => ValueTask.FromResult<IShape>(new Shape()));

        var script = new RequireSettings();
        var resourceManager = new Mock<IResourceManager>();
        resourceManager
            .Setup(manager => manager.RegisterResource("script", MessagingResourceConfiguration.NotificationsScript))
            .Returns(script);

        var agentProfiles = new Mock<IAgentProfileManager>();
        agentProfiles
            .Setup(manager => manager.FindByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request.Agent);

        var filter = new MessagingNotificationsFilter(
            layoutAccessor.Object,
            shapeFactory.Object,
            new PermissionGrants(request.Granted.Select(permission => permission.Name).ToHashSet()),
            agentProfiles.Object,
            resourceManager.Object,
            new OptionsWrapper<AdminOptions>(new AdminOptions()),
            NullLogger<MessagingNotificationsFilter>.Instance);

        var nextCalls = 0;

        await filter.OnResultExecutionAsync(context, () =>
        {
            nextCalls++;

            return Task.FromResult(new ResultExecutedContext(context, [], context.Result, context.Controller));
        });

        var footer = layout.Zones.IsNotEmpty("Footer") ? (Shape)layout.Zones["Footer"] : null;

        return new Outcome(footer?.Items.OfType<IShape>().SingleOrDefault(), script, resourceManager, nextCalls);
    }

    private sealed record Request
    {
        public string Path { get; init; } = "/Admin/contacts";

        public IActionResult Result { get; init; }

        public object Controller { get; init; }

        public string RouteName { get; init; } = "ContactsIndex";

        public AgentProfile Agent { get; init; }

        public Permission[] Granted { get; init; } = [];
    }

    private sealed record Outcome(IShape Shape, RequireSettings Script, Mock<IResourceManager> ResourceManager, int NextCalls);

    // Grants the named permissions and nothing else.
    private sealed class PermissionGrants(ISet<string> granted) : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            var allowed = requirements.OfType<PermissionRequirement>().All(requirement => granted.Contains(requirement.Permission.Name));

            return Task.FromResult(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());
    }
}
