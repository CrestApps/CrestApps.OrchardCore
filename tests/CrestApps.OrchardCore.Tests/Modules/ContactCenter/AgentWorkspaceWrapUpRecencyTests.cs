using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
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
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The docked agent bar prompts the agent to complete the activity behind a call they just finished. That prompt
/// is live after-call work, not a permanent to-do: once a call has been finished longer than the wrap-up window
/// (the same window the availability recovery pass uses to close a stale wrap-up out), it must stop appearing so a
/// past call cannot sit in the bar indefinitely demanding completion.
/// </summary>
public sealed class AgentWorkspaceWrapUpRecencyTests
{
    private static readonly DateTime ClockNow = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task WorkspacePoll_SurfacesWrapUp_WhenCallEndedWithinTheWrapUpWindow()
    {
        // A call that ended a minute ago is live after-call work: the bar should still offer its completion.
        var probe = new WrapUpProbe(endedUtc: ClockNow.AddMinutes(-1));

        var result = await probe.PollAsync();
        var model = Assert.IsType<AgentWorkspaceStateViewModel>(GetValue(result));

        Assert.NotNull(model.ActiveInteraction);
        Assert.Equal("interaction-0001", model.ActiveInteraction.InteractionId);
    }

    [Fact]
    public async Task WorkspacePoll_DropsWrapUp_WhenCallEndedBeforeTheWrapUpWindow()
    {
        // The same interaction, but finished well past the 15-minute wrap-up window: it is no longer live work,
        // so the bar must not keep showing it.
        var probe = new WrapUpProbe(endedUtc: ClockNow.AddHours(-2));

        var result = await probe.PollAsync();
        var model = Assert.IsType<AgentWorkspaceStateViewModel>(GetValue(result));

        Assert.Null(model.ActiveInteraction);
    }

    private static object GetValue(IResult result)
    {
        var property = result.GetType().GetProperty("Value");

        Assert.NotNull(property);

        return property.GetValue(result);
    }

    private sealed class WrapUpProbe
    {
        private readonly Mock<IAgentProfileManager> _agentManager = new();
        private readonly Mock<IActivityReservationManager> _reservationManager = new();
        private readonly Mock<IActivityQueueManager> _queueManager = new();
        private readonly Mock<IQueueItemManager> _queueItemManager = new();
        private readonly Mock<IInteractionManager> _interactionManager = new();
        private readonly Mock<IOmnichannelActivityManager> _activityManager = new();
        private readonly Mock<IContentManager> _contentManager = new();
        private readonly Mock<IDisplayNameProvider> _displayNameProvider = new();

        public WrapUpProbe(DateTime endedUtc)
        {
            var profile = new AgentProfile
            {
                ItemId = "agent-0001",
                UserId = "user-0001",
                DisplayName = "Agent One",
            };

            _agentManager
                .Setup(manager => manager.FindByUserIdAsync("user-0001", It.IsAny<CancellationToken>()))
                .ReturnsAsync(profile);

            _reservationManager
                .Setup(manager => manager.FindPendingByAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ActivityReservation)null);

            // No live call: the handler falls through to the wrap-up check.
            _interactionManager
                .Setup(manager => manager.FindActiveByAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Interaction)null);

            var interaction = new Interaction
            {
                ItemId = "interaction-0001",
                AgentId = "agent-0001",
                ActivityItemId = "activity-0001",
                AnsweredUtc = endedUtc.AddMinutes(-5),
                EndedUtc = endedUtc,
            }.RestorePersistedStatus(InteractionStatus.Ended);

            _interactionManager
                .Setup(manager => manager.GetRecentByAgentAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([interaction]);

            // The activity is still open, so it qualifies as pending wrap-up work whenever it is within the window.
            var activity = new OmnichannelActivity
            {
                ItemId = "activity-0001",
                Status = ActivityStatus.InProgress,
                AssignedToId = "user-0001",
            };

            _activityManager
                .Setup(manager => manager.GetByIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([activity]);

            _activityManager
                .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(activity);
        }

        public Task<IResult> PollAsync()
        {
            return AgentWorkspaceEndpoints.HandleStateAsync(
                new AlwaysAllowAuthorizationService(),
                _agentManager.Object,
                _reservationManager.Object,
                _queueManager.Object,
                _queueItemManager.Object,
                _interactionManager.Object,
                _activityManager.Object,
                _contentManager.Object,
                MockUserManager(),
                _displayNameProvider.Object,
                Mock.Of<IContactCenterVoiceProviderResolver>(),
                new StubClock(ClockNow),
                Options.Create(new AgentAvailabilityOptions()),
                CreateLinkGenerator(),
                CreateHttpContext());
        }

        private sealed class AlwaysAllowAuthorizationService : IAuthorizationService
        {
            public Task<AuthorizationResult> AuthorizeAsync(
                ClaimsPrincipal user,
                object resource,
                IEnumerable<IAuthorizationRequirement> requirements)
                => Task.FromResult(AuthorizationResult.Success());

            public Task<AuthorizationResult> AuthorizeAsync(
                ClaimsPrincipal user,
                object resource,
                string policyName)
                => Task.FromResult(AuthorizationResult.Success());
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
