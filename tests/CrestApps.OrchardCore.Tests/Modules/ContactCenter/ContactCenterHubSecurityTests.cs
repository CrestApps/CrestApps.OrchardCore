using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Hubs;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.SignalR;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Connections.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterHubSecurityTests
{
    private const string TenantName = "Acme";
    private const string UserId = "user-1";
    private const string ConnectionId = "connection-1";

    [Fact]
    public async Task OnConnectedAsync_WhenTheUserHoldsNoContactCenterPermission_AbortsWithoutJoiningAnyGroup()
    {
        // Arrange
        var harness = new HubHarness();

        // Act
        await harness.Hub.OnConnectedAsync();

        // Assert
        Assert.Equal(1, harness.Context.AbortCount);
        Assert.Empty(harness.Groups.Operations);
        Assert.Empty(harness.SessionService.ConnectedUserIds);
    }

    [Fact]
    public async Task OnConnectedAsync_WhenTheUserCanSignIntoQueues_JoinsOnlyTenantQualifiedQueueAndUserGroups()
    {
        // Arrange
        var harness = new HubHarness();
        harness.Grant(ContactCenterPermissions.SignIntoQueues);
        harness.SessionService.SessionQueueIds = ["sales"];
        harness.SessionService.SnapshotQueueIds = ["sales"];

        // Act
        await harness.Hub.OnConnectedAsync();

        // Assert
        Assert.Equal(0, harness.Context.AbortCount);
        Assert.Equal(UserId, Assert.Single(harness.SessionService.ConnectedUserIds));

        Assert.Equal(
            [
                $"add:{TenantSignalRGroupName.ForGroup(TenantName, ContactCenterHub.QueueGroup("sales"))}",
                $"add:{TenantSignalRGroupName.ForUser(TenantName, UserId)}",
            ],
            harness.Groups.Operations);

        Assert.DoesNotContain(
            harness.Groups.Operations,
            operation => operation.Contains(ContactCenterHub.SupervisorsGroup, StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnConnectedAsync_WhenTheUserCannotSignIntoQueues_NeverOpensASessionOrJoinsAQueueGroup()
    {
        // Arrange
        var harness = new HubHarness();
        harness.Grant(ContactCenterPermissions.MonitorContactCenter);
        harness.SessionService.SessionQueueIds = ["sales"];
        harness.SessionService.SnapshotQueueIds = ["sales"];

        // Act
        await harness.Hub.OnConnectedAsync();

        // Assert
        Assert.Equal(0, harness.Context.AbortCount);
        Assert.Empty(harness.SessionService.ConnectedUserIds);

        Assert.Equal(
            [
                $"add:{TenantSignalRGroupName.ForGroup(TenantName, ContactCenterHub.SupervisorsGroup)}",
                $"add:{TenantSignalRGroupName.ForUser(TenantName, UserId)}",
            ],
            harness.Groups.Operations);
    }

    [Fact]
    public async Task OnConnectedAsync_ReconcilesQueueGroupsAgainstTheFreshSnapshot()
    {
        // Arrange
        var harness = new HubHarness();
        harness.Grant(ContactCenterPermissions.SignIntoQueues);
        harness.SessionService.SessionQueueIds = ["sales", "support"];
        harness.SessionService.SnapshotQueueIds = ["support", "billing"];

        // Act
        await harness.Hub.OnConnectedAsync();

        // Assert
        var sales = TenantSignalRGroupName.ForGroup(TenantName, ContactCenterHub.QueueGroup("sales"));
        var support = TenantSignalRGroupName.ForGroup(TenantName, ContactCenterHub.QueueGroup("support"));
        var billing = TenantSignalRGroupName.ForGroup(TenantName, ContactCenterHub.QueueGroup("billing"));

        Assert.Equal(
            [
                $"add:{sales}",
                $"add:{support}",
                $"add:{billing}",
                $"remove:{sales}",
                $"add:{TenantSignalRGroupName.ForUser(TenantName, UserId)}",
            ],
            harness.Groups.Operations);
    }

    [Fact]
    public async Task OnConnectedAsync_WhenTheRealTimeFeatureIsDraining_AbortsBeforeAuthorizing()
    {
        // Arrange
        var harness = new HubHarness();
        harness.Grant(ContactCenterPermissions.SignIntoQueues);
        harness.Grant(ContactCenterPermissions.MonitorContactCenter);
        harness.WorkLeaseGranted = false;

        // Act
        await harness.Hub.OnConnectedAsync();

        // Assert
        Assert.Equal(1, harness.Context.AbortCount);
        Assert.Empty(harness.Groups.Operations);
        Assert.Empty(harness.AuthorizationService.EvaluatedPermissions);
    }

    [Fact]
    public async Task OnConnectedAsync_WhenTheConnectionRegistryIsQuiescing_ReleasesTheWorkLeaseAndJoinsNothing()
    {
        // Arrange
        var harness = new HubHarness();
        harness.Grant(ContactCenterPermissions.SignIntoQueues);
        harness.ConnectionRegistry.Quiesce();

        // Act
        await harness.Hub.OnConnectedAsync();

        // Assert
        Assert.True(harness.WorkLease.IsDisposed);
        Assert.Empty(harness.Groups.Operations);
        Assert.Empty(harness.AuthorizationService.EvaluatedPermissions);
    }

    [Fact]
    public async Task OnConnectedAsync_WhenTheConnectionCarriesNoUserIdentifier_AbortsBeforeAuthorizing()
    {
        // Arrange
        var harness = new HubHarness(userId: null);
        harness.Grant(ContactCenterPermissions.SignIntoQueues);

        // Act
        await harness.Hub.OnConnectedAsync();

        // Assert
        Assert.Equal(1, harness.Context.AbortCount);
        Assert.Empty(harness.Groups.Operations);
        Assert.Empty(harness.AuthorizationService.EvaluatedPermissions);
    }

    [Fact]
    public async Task OnConnectedAsync_WhenTheConnectionHasNoHttpContext_DeniesEveryPermissionAndAborts()
    {
        // Arrange
        var harness = new HubHarness(withHttpContext: false);
        harness.Grant(ContactCenterPermissions.SignIntoQueues);
        harness.Grant(ContactCenterPermissions.MonitorContactCenter);

        // Act
        await harness.Hub.OnConnectedAsync();

        // Assert
        Assert.Equal(1, harness.Context.AbortCount);
        Assert.Empty(harness.Groups.Operations);
        Assert.Empty(harness.AuthorizationService.EvaluatedPermissions);
    }

    [Fact]
    public async Task OnConnectedAsync_WhenTheSessionServiceFails_StillJoinsTheSupervisorAndUserGroups()
    {
        // Arrange
        var harness = new HubHarness();
        harness.Grant(ContactCenterPermissions.SignIntoQueues);
        harness.Grant(ContactCenterPermissions.MonitorContactCenter);
        harness.SessionService.ConnectException = new InvalidOperationException("The agent session store is offline.");

        // Act
        await harness.Hub.OnConnectedAsync();

        // Assert
        Assert.Equal(0, harness.Context.AbortCount);

        Assert.Equal(
            [
                $"add:{TenantSignalRGroupName.ForGroup(TenantName, ContactCenterHub.SupervisorsGroup)}",
                $"add:{TenantSignalRGroupName.ForUser(TenantName, UserId)}",
            ],
            harness.Groups.Operations);
    }

    private sealed class HubHarness
    {
        public HubHarness(string userId = UserId, bool withHttpContext = true)
        {
            AuthorizationService = new RecordingAuthorizationService();
            SessionService = new FakeAgentSessionService();
            ConnectionRegistry = new ContactCenterHubConnectionRegistry();
            Groups = new RecordingGroupManager();
            WorkLease = new FakeWorkLease();

            var services = new ServiceCollection()
                .AddSingleton<IAuthorizationService>(AuthorizationService)
                .AddSingleton<IAgentSessionService>(SessionService)
                .AddSingleton(Mock.Of<IAgentPresenceManager>())
                .AddSingleton(Mock.Of<ISupervisorQueueAuthorizationService>())
                .AddSingleton(MockUserManager())
                .AddSingleton(Mock.Of<IDisplayNameProvider>())
                .AddTransient<ContactCenterHubScopeContext>()
                .BuildServiceProvider();

            var workManager = new Mock<IContactCenterFeatureWorkManager>();
            workManager
                .Setup(manager => manager.TryEnter(ContactCenterConstants.Feature.RealTime))
                .Returns(() => WorkLeaseGranted
                    ? WorkLease
                    : null);

            Context = new TestHubCallerContext(userId, withHttpContext);

            Hub = new ContactCenterHub(
                NullLogger<ContactCenterHub>.Instance,
                new TestContactCenterScopeExecutor(services),
                workManager.Object,
                ConnectionRegistry,
                new ShellSettings { Name = TenantName })
            {
                Context = Context,
                Groups = Groups,
            };
        }

        public ContactCenterHub Hub { get; }

        public TestHubCallerContext Context { get; }

        public RecordingGroupManager Groups { get; }

        public RecordingAuthorizationService AuthorizationService { get; }

        public FakeAgentSessionService SessionService { get; }

        public ContactCenterHubConnectionRegistry ConnectionRegistry { get; }

        public FakeWorkLease WorkLease { get; }

        public bool WorkLeaseGranted { get; set; } = true;

        public void Grant(Permission permission)
        {
            AuthorizationService.GrantedPermissions.Add(permission.Name);
        }

        private static UserManager<IUser> MockUserManager()
        {
            var store = new Mock<IUserStore<IUser>>();

            return new Mock<UserManager<IUser>>(
                store.Object,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null).Object;
        }
    }

    private sealed class TestHubCallerContext : HubCallerContext
    {
        private readonly string _userId;

        public TestHubCallerContext(string userId, bool withHttpContext)
        {
            _userId = userId;

            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId ?? string.Empty),
                    new Claim(ClaimTypes.Name, "agent@example.com"),
                ],
                "Test"));

            User = principal;
            Features = new FeatureCollection();

            if (withHttpContext)
            {
                Features.Set<IHttpContextFeature>(new TestHttpContextFeature
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = principal,
                    },
                });
            }
        }

        public int AbortCount { get; private set; }

        public override string ConnectionId => ContactCenterHubSecurityTests.ConnectionId;

        public override string UserIdentifier => _userId;

        public override ClaimsPrincipal User { get; }

        public override IDictionary<object, object> Items { get; } = new Dictionary<object, object>();

        public override IFeatureCollection Features { get; }

        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public override void Abort()
        {
            AbortCount++;
        }
    }

    private sealed class TestHttpContextFeature : IHttpContextFeature
    {
        public HttpContext HttpContext { get; set; }
    }

    private sealed class RecordingGroupManager : IGroupManager
    {
        public List<string> Operations { get; } = [];

        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Assert.Equal(ConnectionId, connectionId);
            Operations.Add($"add:{groupName}");

            return Task.CompletedTask;
        }

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
        {
            Assert.Equal(ConnectionId, connectionId);
            Operations.Add($"remove:{groupName}");

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAuthorizationService : IAuthorizationService
    {
        public HashSet<string> GrantedPermissions { get; } = new(StringComparer.Ordinal);

        public List<string> EvaluatedPermissions { get; } = [];

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            IEnumerable<IAuthorizationRequirement> requirements)
        {
            var granted = false;

            foreach (var requirement in requirements)
            {
                if (requirement is not PermissionRequirement permissionRequirement)
                {
                    continue;
                }

                EvaluatedPermissions.Add(permissionRequirement.Permission.Name);

                if (GrantedPermissions.Contains(permissionRequirement.Permission.Name))
                {
                    granted = true;
                }
            }

            return Task.FromResult(granted
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
        {
            EvaluatedPermissions.Add(policyName);

            return Task.FromResult(GrantedPermissions.Contains(policyName)
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());
        }
    }

    private sealed class FakeAgentSessionService : IAgentSessionService
    {
        public List<string> ConnectedUserIds { get; } = [];

        public IList<string> SessionQueueIds { get; set; } = [];

        public IList<string> SnapshotQueueIds { get; set; } = [];

        public Exception ConnectException { get; set; }

        public Task<AgentSession> ConnectAsync(
            string userId,
            string connectionId,
            string userName,
            string displayName,
            CancellationToken cancellationToken = default)
        {
            if (ConnectException is not null)
            {
                throw ConnectException;
            }

            ConnectedUserIds.Add(userId);

            return Task.FromResult(new AgentSession
            {
                UserId = userId,
                ConnectionIds = [connectionId],
                QueueIds = SessionQueueIds,
            });
        }

        public Task<AgentSession> DisconnectAsync(string userId, string connectionId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AgentSession { UserId = userId });
        }

        public Task<AgentSession> HeartbeatAsync(string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AgentSession { UserId = userId });
        }

        public Task<AgentDesktopSnapshot> BuildSnapshotAsync(string userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AgentDesktopSnapshot
            {
                UserId = userId,
                QueueIds = SnapshotQueueIds,
            });
        }

        public Task<int> ExpireStaleAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }
    }

    private sealed class FakeWorkLease : IContactCenterFeatureWorkLease
    {
        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }
}
