using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Hubs;
using CrestApps.OrchardCore.SignalR.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

// Every admin page listens on the messaging hub for toasts, passively. Only the workspace is presence: counting a
// passive page would let routed distribution push conversations at an agent with no inbox on screen.
public sealed class MessagingHubPresenceTests
{
    private const string TenantName = "Default";
    private const string UserId = "user-1";
    private const string AgentId = "agent-1";

    [Fact]
    public async Task OnConnected_Passively_JoinsTheAgentsGroupsWithoutRecordingPresence()
    {
        var harness = CreateHarness(passive: true);

        await harness.Hub.OnConnectedAsync();

        harness.Presence.Verify(tracker => tracker.TouchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains(Group(MessagingHub.AgentGroup(AgentId)), harness.JoinedGroups);
        Assert.Contains(Group(MessagingHub.QueueGroup("queue-1")), harness.JoinedGroups);
    }

    // A queue's group hears every new message in the queue, with the customer's address and a preview, so only a role
    // that may see the queue's shared inbox joins it.
    [Fact]
    public async Task OnConnected_WithoutTheQueuePermission_JoinsOnlyTheAgentsOwnGroup()
    {
        var harness = CreateHarness(passive: true, grantQueues: false);

        await harness.Hub.OnConnectedAsync();

        Assert.Contains(Group(MessagingHub.AgentGroup(AgentId)), harness.JoinedGroups);
        Assert.DoesNotContain(Group(MessagingHub.QueueGroup("queue-1")), harness.JoinedGroups);
    }

    [Fact]
    public async Task OnConnected_FromTheWorkspace_RecordsPresence()
    {
        var harness = CreateHarness(passive: false);

        await harness.Hub.OnConnectedAsync();

        harness.Presence.Verify(tracker => tracker.TouchAsync(AgentId, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Contains(Group(MessagingHub.AgentGroup(AgentId)), harness.JoinedGroups);
    }

    [Fact]
    public async Task Heartbeat_OverAPassiveConnection_NeverRecordsPresence()
    {
        var harness = CreateHarness(passive: true);

        await harness.Hub.OnConnectedAsync();
        await harness.Hub.Heartbeat();

        harness.Presence.Verify(tracker => tracker.TouchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Heartbeat_FromTheWorkspace_RecordsPresence()
    {
        var harness = CreateHarness(passive: false);

        await harness.Hub.OnConnectedAsync();
        await harness.Hub.Heartbeat();

        harness.Presence.Verify(tracker => tracker.TouchAsync(AgentId, It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task OnConnected_WithoutTheWorkspacePermission_IsRefused()
    {
        var harness = CreateHarness(passive: true, grantWorkspace: false);

        await harness.Hub.OnConnectedAsync();

        harness.Context.Verify(context => context.Abort(), Times.Once);
        Assert.Empty(harness.JoinedGroups);
    }

    private static string Group(string name) => TenantSignalRGroupName.ForGroup(TenantName, name);

    private static Harness CreateHarness(bool passive, bool grantWorkspace = true, bool grantQueues = true)
    {
        var agentProfiles = new Mock<IAgentProfileManager>();
        agentProfiles
            .Setup(manager => manager.FindByUserIdAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = AgentId, UserId = UserId, QueueIds = ["queue-1"] });

        var presence = new Mock<IMessagingPresenceTracker>();
        var joined = new List<string>();
        var groups = new Mock<IGroupManager>();
        groups
            .Setup(manager => manager.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string _, string group, CancellationToken _) => joined.Add(group))
            .Returns(Task.CompletedTask);

        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UserId)], "Test"));
        var httpContext = new DefaultHttpContext { User = user };

        if (passive)
        {
            httpContext.Request.QueryString = new QueryString($"?{MessagingHub.PassiveQueryParameter}=1");
        }

        var features = new FeatureCollection();
        features.Set<IHttpContextFeature>(new TestHttpContextFeature(httpContext));

        var context = new Mock<HubCallerContext>();
        context.SetupGet(value => value.ConnectionId).Returns("connection-1");
        context.SetupGet(value => value.ConnectionAborted).Returns(TestContext.Current.CancellationToken);
        context.SetupGet(value => value.Features).Returns(features);
        context.SetupGet(value => value.User).Returns(user);
        context.SetupGet(value => value.UserIdentifier).Returns(UserId);
        context.SetupGet(value => value.Items).Returns(new Dictionary<object, object>());

        var granted = grantWorkspace
            ? new HashSet<string> { MessagingPermissions.UseMessagingWorkspace.Name }
            : [];

        if (grantWorkspace)
        {
            // The Agent role: their own conversations, and their queues' unclaimed ones unless the test takes them away.
            granted.Add(grantQueues ? MessagingPermissions.ViewQueueConversations.Name : MessagingPermissions.ViewOwnConversations.Name);
        }

        var hub = new MessagingHub(
            agentProfiles.Object,
            new PermissiveAgentEntitlementPolicy(),
            new PermissionGrants(granted),
            presence.Object,
            new ShellSettings { Name = TenantName },
            NullLogger<MessagingHub>.Instance)
        {
            Context = context.Object,
            Groups = groups.Object,
        };

        return new Harness(hub, presence, context, joined);
    }

    private sealed record Harness(MessagingHub Hub, Mock<IMessagingPresenceTracker> Presence, Mock<HubCallerContext> Context, List<string> JoinedGroups);

    private sealed class TestHttpContextFeature(HttpContext httpContext) : IHttpContextFeature
    {
        public HttpContext HttpContext { get; set; } = httpContext;
    }

    // Grants the named permissions, and what they imply, and nothing else.
    private sealed class PermissionGrants(ISet<string> granted) : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
        {
            var allowed = requirements.OfType<PermissionRequirement>().All(requirement => IsGranted(requirement.Permission));

            return Task.FromResult(allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        }

        private bool IsGranted(Permission permission)
            => permission is not null && (granted.Contains(permission.Name) || (permission.ImpliedBy ?? []).Any(IsGranted));

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());
    }
}
