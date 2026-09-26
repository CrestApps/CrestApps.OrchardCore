using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Moq;
using OrchardCore.Security;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

public sealed class MessagingTransferTargetsTests
{
    [Fact]
    public async Task SearchAgentsAsync_ListsOtherMessagingAgentsByName_LeavingOutTheCurrentHolder()
    {
        var targets = new Builder()
            .Agent("agent-a", "Ann Sender")
            .Agent("agent-c", "Cal Colleague")
            .Agent("agent-b", "Bea Recipient")
            .Build();

        var options = await targets.SearchAgentsAsync(HeldBy("agent-a"), query: null, TestContext.Current.CancellationToken);

        Assert.Equal(["Bea Recipient", "Cal Colleague"], options.Select(option => option.Text));
        Assert.Equal(["agent-b", "agent-c"], options.Select(option => option.Value));
    }

    [Fact]
    public async Task SearchAgentsAsync_LeavesOutPeopleWhoCannotUseTheMessagingWorkspace()
    {
        var targets = new Builder()
            .Agent("agent-b", "Bea Recipient")
            .Agent("agent-v", "Val VoiceOnly", canUseMessaging: false)
            .Agent("agent-x", "No User", hasUser: false)
            .Build();

        var options = await targets.SearchAgentsAsync(HeldBy("agent-a"), query: null, TestContext.Current.CancellationToken);

        Assert.Equal(["Bea Recipient"], options.Select(option => option.Text));
    }

    [Fact]
    public async Task SearchAgentsAsync_MatchesAnyPartOfTheNameIgnoringCase()
    {
        var targets = new Builder()
            .Agent("agent-b", "Bea Recipient")
            .Agent("agent-c", "Cal Colleague")
            .Build();

        var options = await targets.SearchAgentsAsync(HeldBy("agent-a"), query: "  recip ", TestContext.Current.CancellationToken);

        Assert.Equal(["agent-b"], options.Select(option => option.Value));
    }

    [Fact]
    public async Task IsEligibleAgentAsync_RefusesSomeoneWithoutTheWorkspacePermission()
    {
        var targets = new Builder()
            .Agent("agent-b", "Bea Recipient")
            .Agent("agent-v", "Val VoiceOnly", canUseMessaging: false)
            .Build();

        Assert.True(await targets.IsEligibleAgentAsync("agent-b", TestContext.Current.CancellationToken));
        Assert.False(await targets.IsEligibleAgentAsync("agent-v", TestContext.Current.CancellationToken));
        Assert.False(await targets.IsEligibleAgentAsync("agent-missing", TestContext.Current.CancellationToken));
        Assert.False(await targets.IsEligibleAgentAsync(null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SearchQueuesAsync_ListsEnabledTeams_LeavingOutTheOneWhosePoolItAlreadyWaitsIn()
    {
        var targets = new Builder()
            .Queue("queue-1", "Billing")
            .Queue("queue-2", "Accounts")
            .Queue("queue-3", "Closed desk", enabled: false)
            .Queue(ContactCenterConstants.DirectRouting.QueueId, "Direct")
            .Build();

        var pooled = new MessagingConversation
        {
            OwnerType = ConversationOwnerType.Queue,
            OwnerId = "queue-1",
            AssignmentStatus = ConversationAssignmentStatus.Pooled,
        };

        var options = await targets.SearchQueuesAsync(pooled, query: null, TestContext.Current.CancellationToken);

        Assert.Equal(["Accounts"], options.Select(option => option.Text));
    }

    [Fact]
    public async Task SearchQueuesAsync_OffersTheOwningTeam_WhenSomebodyHoldsTheConversation()
    {
        var targets = new Builder()
            .Queue("queue-1", "Billing")
            .Build();

        var held = new MessagingConversation
        {
            OwnerType = ConversationOwnerType.Queue,
            OwnerId = "queue-1",
            AssignedAgentId = "agent-a",
            AssignmentStatus = ConversationAssignmentStatus.Assigned,
        };

        var options = await targets.SearchQueuesAsync(held, query: null, TestContext.Current.CancellationToken);

        Assert.Equal(["queue-1"], options.Select(option => option.Value));
    }

    [Fact]
    public async Task SearchQueuesAsync_WhenQueuesAreNotAvailable_ReturnsNothing()
    {
        var targets = new Builder().WithoutQueues().Build();

        Assert.False(targets.SupportsQueues);
        Assert.Empty(await targets.SearchQueuesAsync(HeldBy("agent-a"), query: null, TestContext.Current.CancellationToken));
    }

    private static MessagingConversation HeldBy(string agentId)
        => new()
        {
            OwnerType = ConversationOwnerType.Personal,
            OwnerId = agentId,
            AssignedAgentId = agentId,
            AssignmentStatus = ConversationAssignmentStatus.Assigned,
        };

    private sealed class Builder
    {
        private readonly List<AgentProfile> _agents = [];
        private readonly Dictionary<string, (IUser User, string Name, bool CanUseMessaging)> _users = new(StringComparer.Ordinal);
        private readonly List<ActivityQueue> _queues = [];
        private bool _withQueues = true;

        public Builder Agent(string id, string name, bool canUseMessaging = true, bool hasUser = true)
        {
            var userId = "user-" + id;

            _agents.Add(new AgentProfile { ItemId = id, UserId = userId });

            if (hasUser)
            {
                var user = new Mock<IUser>();
                user.SetupGet(instance => instance.UserName).Returns(userId);
                _users[userId] = (user.Object, name, canUseMessaging);
            }

            return this;
        }

        public Builder Queue(string id, string name, bool enabled = true)
        {
            _queues.Add(new ActivityQueue { ItemId = id, Name = name, Enabled = enabled });

            return this;
        }

        public Builder WithoutQueues()
        {
            _withQueues = false;

            return this;
        }

        public MessagingTransferTargets Build()
        {
            var agents = new Mock<IAgentProfileManager>();
            agents.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_agents);
            agents
                .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => _agents.FirstOrDefault(agent => agent.ItemId == id));

            var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
            userManager
                .Setup(manager => manager.FindByIdAsync(It.IsAny<string>()))
                .ReturnsAsync((string userId) => _users.TryGetValue(userId, out var entry) ? entry.User : null);

            var principalFactory = new Mock<IUserClaimsPrincipalFactory<IUser>>();
            principalFactory
                .Setup(factory => factory.CreateAsync(It.IsAny<IUser>()))
                .ReturnsAsync((IUser user) => new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, user.UserName)], "Test")));

            var authorization = new Mock<IAuthorizationService>();
            authorization
                .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
                .ReturnsAsync((ClaimsPrincipal principal, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
                {
                    var asksWorkspace = requirements
                        .OfType<PermissionRequirement>()
                        .Any(requirement => requirement.Permission.Name == MessagingPermissions.UseMessagingWorkspace.Name);

                    var allowed = asksWorkspace &&
                        _users.TryGetValue(principal.Identity.Name, out var entry) &&
                        entry.CanUseMessaging;

                    return allowed ? AuthorizationResult.Success() : AuthorizationResult.Failed();
                });

            var displayNames = new Mock<IDisplayNameProvider>();
            displayNames
                .Setup(provider => provider.GetAsync(It.IsAny<IUser>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IUser user, CancellationToken _) => _users[user.UserName].Name);

            var queueManagers = new List<IActivityQueueManager>();

            if (_withQueues)
            {
                var queues = new Mock<IActivityQueueManager>();
                queues.Setup(manager => manager.GetEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_queues);
                queueManagers.Add(queues.Object);
            }

            return new MessagingTransferTargets(
                agents.Object,
                queueManagers,
                userManager.Object,
                principalFactory.Object,
                authorization.Object,
                displayNames.Object);
        }
    }
}
