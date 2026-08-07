using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Asserts a round-trip budget for the agent workspace poll. Every signed-in agent polls this endpoint
/// continuously, so a read that costs a query per queue the agent covers, or per interaction the agent has just
/// finished, makes the cost of running a contact center grow with the size of the contact center. The batching
/// this endpoint depends on lives in the stores, so a handler that looped over the single-item APIs instead
/// would return byte-identical state and no functional test could see the difference: only counting the calls
/// one poll makes distinguishes them.
/// </summary>
public sealed class AgentWorkspaceRoundTripBudgetTests
{
    private const int QueueCount = 12;
    private const int RecentInteractionCount = 8;

    [Fact]
    public async Task WorkspacePoll_ReadsQueueDepthOnceForEveryQueueTheAgentCovers()
    {
        // Arrange
        var context = new WorkspaceProbe();

        // Act
        await context.PollAsync();

        // Assert
        context.QueueItemManager.Verify(
            manager => manager.CountWaitingByQueueIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // The single-queue count is what the batched read replaced. Reaching for it here is the regression.
        context.QueueItemManager.Verify(
            manager => manager.CountWaitingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task WorkspacePoll_ResolvesTheActivitiesBehindRecentWorkTogether()
    {
        // Arrange
        var context = new WorkspaceProbe();

        // Act
        await context.PollAsync();

        // Assert
        // The wrap-up check inspects the activity behind every interaction the agent recently finished. One
        // batched read is the budget; the number of reads must not follow the number of interactions.
        context.ActivityManager.Verify(
            manager => manager.ListByIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);

        context.ActivityManager.Verify(
            manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtMostOnce);
    }

    [Fact]
    public async Task WorkspacePoll_ReadsRecentInteractionsOnce()
    {
        // Arrange
        // The active-interaction panel and the history panel are built from the same recent interactions. Each
        // reading them for itself is not an N+1 — it is a constant factor — but it is the same unbounded query
        // run twice on every poll of every agent.
        var context = new WorkspaceProbe();

        // Act
        await context.PollAsync();

        // Assert
        context.InteractionManager.Verify(
            manager => manager.ListRecentByAgentAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task WorkspacePoll_ReportsTheQueueDepthsTheBatchedReadReturned()
    {
        // A budget on its own is satisfied by a handler that issues one query and then ignores the answer, so
        // the numbers the poll reports are asserted against the numbers the batch returned.
        var context = new WorkspaceProbe();

        var result = await context.PollAsync();
        var model = Assert.IsType<AgentWorkspaceStateViewModel>(GetValue(result));

        Assert.Equal(QueueCount, model.Queues.Count);

        foreach (var queue in model.Queues)
        {
            Assert.Equal(context.ExpectedWaiting[queue.Id], queue.WaitingCount);
        }
    }

    private static object GetValue(IResult result)
    {
        var property = result.GetType().GetProperty("Value");

        Assert.NotNull(property);

        return property.GetValue(result);
    }

    private sealed class WorkspaceProbe
    {
        public Mock<IQueueItemManager> QueueItemManager { get; } = new();

        public Mock<IOmnichannelActivityManager> ActivityManager { get; } = new();

        public Mock<IInteractionManager> InteractionManager { get; } = new();

        public Dictionary<string, int> ExpectedWaiting { get; } = new(StringComparer.Ordinal);

        private readonly Mock<IAgentProfileManager> _agentManager = new();
        private readonly Mock<IActivityReservationManager> _reservationManager = new();
        private readonly Mock<IActivityQueueManager> _queueManager = new();
        private readonly Mock<IContentManager> _contentManager = new();
        private readonly Mock<IDisplayNameProvider> _displayNameProvider = new();

        public WorkspaceProbe()
        {
            var queueIds = Enumerable.Range(0, QueueCount).Select(index => $"queue-{index:D4}").ToList();

            for (var index = 0; index < queueIds.Count; index++)
            {
                // Distinct depths: equal ones would pass even if the handler reported one queue's depth for all.
                ExpectedWaiting[queueIds[index]] = index + 1;
            }

            var profile = new AgentProfile
            {
                ItemId = "agent-0001",
                UserId = "user-0001",
                DisplayName = "Agent One",
            };

            foreach (var queueId in queueIds)
            {
                profile.QueueIds.Add(queueId);
            }

            _agentManager
                .Setup(manager => manager.FindByUserIdAsync("user-0001", It.IsAny<CancellationToken>()))
                .ReturnsAsync(profile);

            _queueManager
                .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => new ActivityQueue
                {
                    ItemId = id,
                    Name = id,
                });

            QueueItemManager
                .Setup(manager => manager.CountWaitingByQueueIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<string> ids, CancellationToken _) =>
                    ids.ToDictionary(id => id, id => ExpectedWaiting[id], StringComparer.Ordinal));

            QueueItemManager
                .Setup(manager => manager.CountWaitingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => ExpectedWaiting[id]);

            // No active interaction, so the handler falls through to the wrap-up check — which is the path that
            // reads an activity per recently finished interaction.
            InteractionManager
                .Setup(manager => manager.FindActiveByAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Interaction)null);

            var recent = Enumerable.Range(0, RecentInteractionCount)
                .Select(index => new Interaction
                {
                    ItemId = $"interaction-{index:D4}",
                    AgentId = "agent-0001",
                    ActivityItemId = $"activity-{index:D4}",
                }.RestorePersistedStatus(InteractionStatus.Ended))
                .ToArray();

            InteractionManager
                .Setup(manager => manager.ListRecentByAgentAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(recent);

            // Every activity is already completed, so no interaction qualifies as pending wrap-up. That keeps
            // the poll on the path where the whole batch has to be inspected, which is the worst case.
            ActivityManager
                .Setup(manager => manager.ListByIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<string> ids, CancellationToken _) => [.. ids.Select(id => new OmnichannelActivity
                {
                    ItemId = id,
                    Status = ActivityStatus.Completed,
                })]);

            ActivityManager
                .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => new OmnichannelActivity
                {
                    ItemId = id,
                    Status = ActivityStatus.Completed,
                });

            _reservationManager
                .Setup(manager => manager.FindPendingByAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ActivityReservation)null);
        }

        public Task<IResult> PollAsync()
        {
            return AgentWorkspaceEndpoints.HandleStateAsync(
                new TestAuthorizationService(true),
                _agentManager.Object,
                _reservationManager.Object,
                _queueManager.Object,
                QueueItemManager.Object,
                InteractionManager.Object,
                ActivityManager.Object,
                _contentManager.Object,
                MockUserManager(),
                _displayNameProvider.Object,
                new StubClock(),
                CreateLinkGenerator(),
                CreateHttpContext());
        }

        private sealed class TestAuthorizationService : IAuthorizationService
        {
            private readonly bool _isAuthorized;

            public TestAuthorizationService(bool isAuthorized)
            {
                _isAuthorized = isAuthorized;
            }

            public Task<AuthorizationResult> AuthorizeAsync(
                ClaimsPrincipal user,
                object resource,
                IEnumerable<IAuthorizationRequirement> requirements)
            {
                return Task.FromResult(_isAuthorized
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());
            }

            public Task<AuthorizationResult> AuthorizeAsync(
                ClaimsPrincipal user,
                object resource,
                string policyName)
            {
                return Task.FromResult(_isAuthorized
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());
            }
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

        private static LinkGenerator CreateLinkGenerator()
            => new ServiceCollection()
                .AddLogging()
                .AddRouting()
                .BuildServiceProvider()
                .GetRequiredService<LinkGenerator>();

        private static DefaultHttpContext CreateHttpContext()
        {
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-0001")], "Test");

            return new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
                RequestServices = new ServiceCollection().BuildServiceProvider(),
            };
        }
    }
}
