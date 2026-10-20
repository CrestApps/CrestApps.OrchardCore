using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
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
/// Live: a power dial with answering-machine screening reserved the agent and held them Busy from the moment it was
/// placed, so the agent bar read "On a call" while the customer was only ringing, and again while their answer was
/// screened. The tester took it that they were already on the call and did not answer their phone. The workspace state
/// now says where the dial stands -- dialing, screening, connecting -- and leaves "On a call" to a connected agent.
/// </summary>
public sealed class AgentWorkspacePendingDialTests
{
    private static readonly DateTime ClockNow = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GetAttemptPhase_WhileTheCustomerRings_IsDialing()
    {
        var interaction = CampaignDial(InteractionStatus.Ringing);

        Assert.Equal(DialAttemptPhase.Dialing, DialerCallMetadata.GetAttemptPhase(interaction));
    }

    [Fact]
    public void GetAttemptPhase_AnsweredAndWaitingForTheMachineVerdict_IsScreening()
    {
        var interaction = CampaignDial(InteractionStatus.Connected, answered: true, screened: true);

        Assert.Equal(DialAttemptPhase.Screening, DialerCallMetadata.GetAttemptPhase(interaction));
    }

    // A machine's answer is hung up without connecting the agent; until it is, the agent is still waiting on screening.
    [Theory]
    [InlineData(nameof(AnswerClassification.Machine))]
    [InlineData(nameof(AnswerClassification.Fax))]
    public void GetAttemptPhase_AnsweredByAMachine_StaysScreening(string classification)
    {
        var interaction = CampaignDial(InteractionStatus.Connected, answered: true, screened: true, classification: classification);

        Assert.Equal(DialAttemptPhase.Screening, DialerCallMetadata.GetAttemptPhase(interaction));
    }

    [Fact]
    public void GetAttemptPhase_APersonAnsweredTheScreenedCall_IsConnecting()
    {
        var interaction = CampaignDial(InteractionStatus.Connected, answered: true, screened: true, classification: nameof(AnswerClassification.Human));

        Assert.Equal(DialAttemptPhase.Connecting, DialerCallMetadata.GetAttemptPhase(interaction));
    }

    // Without screening an answer is a person's, so there is no "Screening…" step to show.
    [Fact]
    public void GetAttemptPhase_AnsweredWithoutScreening_IsConnecting()
    {
        var interaction = CampaignDial(InteractionStatus.Connected, answered: true);

        Assert.Equal(DialAttemptPhase.Connecting, DialerCallMetadata.GetAttemptPhase(interaction));
    }

    [Fact]
    public void GetAttemptPhase_OnceTheAgentJoined_IsNone()
    {
        var interaction = CampaignDial(InteractionStatus.Connected, answered: true, screened: true, classification: nameof(AnswerClassification.Human));
        DialerCallMetadata.MarkAgentJoined(interaction, ClockNow);

        Assert.Equal(DialAttemptPhase.None, DialerCallMetadata.GetAttemptPhase(interaction));
    }

    [Fact]
    public void GetAttemptPhase_ACallTheDialerDidNotPlace_IsNone()
    {
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            Direction = InteractionDirection.Outbound,
            ActivityItemId = "activity-1",
        }.RestorePersistedStatus(InteractionStatus.Ringing);

        Assert.Equal(DialAttemptPhase.None, DialerCallMetadata.GetAttemptPhase(interaction));
    }

    [Theory]
    [InlineData(InteractionStatus.Ringing, false, false, null, DialAttemptPhase.Dialing)]
    [InlineData(InteractionStatus.Connected, true, true, null, DialAttemptPhase.Screening)]
    [InlineData(InteractionStatus.Connected, true, true, nameof(AnswerClassification.Human), DialAttemptPhase.Connecting)]
    public async Task WorkspacePoll_AnAutomaticDialTheAgentIsHeldFor_ShowsWhereItStandsInsteadOfACall(
        InteractionStatus status,
        bool answered,
        bool screened,
        string classification,
        DialAttemptPhase expected)
    {
        // Arrange
        var probe = new WorkspaceProbe(
            AgentPresenceStatus.Reserved,
            active: CampaignDial(status, answered, screened, classification),
            dialerProfile: new DialerProfile { ItemId = "profile-1", ShowDialProgressToAgent = true });

        // Act
        var model = await probe.PollAsync();

        // Assert
        Assert.Null(model.ActiveInteraction);
        Assert.NotNull(model.PendingDial);
        Assert.True(model.PendingDial.ShowsProgress);
        Assert.Equal(expected, model.PendingDial.Phase);
        Assert.Equal("interaction-1", model.PendingDial.InteractionId);
        Assert.Equal("+17025550100", model.PendingDial.CustomerAddress);
    }

    // Most automated dials never reach a person, so by default the agent is not shown each one: only that they are
    // waiting for a connected call, without the customer, as contact-center dialers show it.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorkspacePoll_AnAutomaticDialWhoseProfileDoesNotShowEachDial_LeavesTheCustomerOut(bool profileKnown)
    {
        // Arrange
        var probe = new WorkspaceProbe(
            AgentPresenceStatus.Reserved,
            active: CampaignDial(InteractionStatus.Ringing),
            dialerProfile: profileKnown ? new DialerProfile { ItemId = "profile-1" } : null);

        // Act
        var model = await probe.PollAsync();

        // Assert
        Assert.Null(model.ActiveInteraction);
        Assert.NotNull(model.PendingDial);
        Assert.False(model.PendingDial.ShowsProgress);
        Assert.Equal(DialAttemptPhase.Dialing, model.PendingDial.Phase);
        Assert.Null(model.PendingDial.CustomerAddress);
        Assert.Null(model.PendingDial.CustomerLabel);
    }

    // Once the agent's leg is on the call it is their call: the card, the timer and "On a call".
    [Fact]
    public async Task WorkspacePoll_OnceTheAgentIsConnected_ShowsTheCall()
    {
        // Arrange
        var interaction = CampaignDial(InteractionStatus.Connected, answered: true, screened: true, classification: nameof(AnswerClassification.Human));
        DialerCallMetadata.MarkAgentJoined(interaction, ClockNow);
        var probe = new WorkspaceProbe(AgentPresenceStatus.Busy, active: interaction);

        // Act
        var model = await probe.PollAsync();

        // Assert
        Assert.Null(model.PendingDial);
        Assert.NotNull(model.ActiveInteraction);
        Assert.Equal("interaction-1", model.ActiveInteraction.InteractionId);
    }

    // A preview dial is the agent's own call from the moment they dial it; it keeps its call card.
    [Fact]
    public async Task WorkspacePoll_APreviewDial_KeepsItsCallCard()
    {
        // Arrange
        var probe = new WorkspaceProbe(
            AgentPresenceStatus.Busy,
            active: CampaignDial(InteractionStatus.Ringing),
            activitySource: ActivitySources.PreviewDial);

        // Act
        var model = await probe.PollAsync();

        // Assert
        Assert.Null(model.PendingDial);
        Assert.NotNull(model.ActiveInteraction);
    }

    // The attempt ended (no answer, or a machine) and the agent's release has not landed yet. The bar keeps showing the
    // dial rather than flashing "On a call" -- or a "Call ended" card for a call the agent was never on.
    [Fact]
    public async Task WorkspacePoll_AnUnconnectedAttemptEndedBeforeTheAgentIsReleased_StillShowsTheDial()
    {
        // Arrange
        var ended = CampaignDial(InteractionStatus.Ended, answered: true, screened: true, classification: nameof(AnswerClassification.Machine));
        ended.EndedUtc = ClockNow.AddSeconds(-2);
        var probe = new WorkspaceProbe(
            AgentPresenceStatus.Busy,
            active: null,
            recent: ended,
            presenceChangedUtc: ClockNow.AddSeconds(-20));

        // Act
        var model = await probe.PollAsync();

        // Assert
        Assert.Null(model.ActiveInteraction);
        Assert.NotNull(model.PendingDial);
        Assert.Equal(DialAttemptPhase.Screening, model.PendingDial.Phase);
    }

    [Fact]
    public async Task WorkspacePoll_AnUnconnectedAttemptAfterTheAgentIsReleased_ShowsNothing()
    {
        // Arrange
        var ended = CampaignDial(InteractionStatus.Ended);
        ended.EndedUtc = ClockNow.AddSeconds(-2);
        var probe = new WorkspaceProbe(
            AgentPresenceStatus.Available,
            active: null,
            recent: ended,
            presenceChangedUtc: ClockNow.AddSeconds(-1));

        // Act
        var model = await probe.PollAsync();

        // Assert
        Assert.Null(model.ActiveInteraction);
        Assert.Null(model.PendingDial);
    }

    // The agent was released and reserved for the next dial before its call exists: the last attempt is not theirs now.
    [Fact]
    public async Task WorkspacePoll_AnEndedAttemptFromBeforeTheAgentsLastStateChange_ShowsNothing()
    {
        // Arrange
        var ended = CampaignDial(InteractionStatus.Ended);
        ended.EndedUtc = ClockNow.AddSeconds(-5);
        var probe = new WorkspaceProbe(
            AgentPresenceStatus.Busy,
            active: null,
            recent: ended,
            presenceChangedUtc: ClockNow.AddSeconds(-1));

        // Act
        var model = await probe.PollAsync();

        // Assert
        Assert.Null(model.PendingDial);
    }

    [Fact]
    public async Task WorkspacePoll_AnEndedAttemptLongAgo_ShowsNothing()
    {
        // Arrange
        var ended = CampaignDial(InteractionStatus.Ended);
        ended.EndedUtc = ClockNow.AddMinutes(-5);
        var probe = new WorkspaceProbe(
            AgentPresenceStatus.Busy,
            active: null,
            recent: ended,
            presenceChangedUtc: ClockNow.AddMinutes(-6));

        // Act
        var model = await probe.PollAsync();

        // Assert
        Assert.Null(model.PendingDial);
    }

    private static Interaction CampaignDial(
        InteractionStatus status,
        bool answered = false,
        bool screened = false,
        string classification = null)
    {
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            AgentId = "agent-1",
            ActivityItemId = "activity-1",
            Direction = InteractionDirection.Outbound,
            CustomerAddress = "+17025550100",
            StartedUtc = ClockNow.AddSeconds(-10),
            AnsweredUtc = answered ? ClockNow.AddSeconds(-3) : null,
        }.RestorePersistedStatus(status);

        interaction.TechnicalMetadata[DialerCallMetadata.DialerProfileIdKey] = "profile-1";

        if (screened)
        {
            DialerCallMetadata.MarkAnswerDetectionRequested(interaction);
        }

        if (classification is not null)
        {
            interaction.TechnicalMetadata[ContactCenterConstants.TelephonyMetadata.AnswerClassification] = classification;
        }

        return interaction;
    }

    private sealed class WorkspaceProbe
    {
        private readonly Mock<IAgentProfileManager> _agentManager = new();
        private readonly Mock<IActivityReservationManager> _reservationManager = new();
        private readonly Mock<IActivityQueueManager> _queueManager = new();
        private readonly Mock<IQueueItemManager> _queueItemManager = new();
        private readonly Mock<IInteractionManager> _interactionManager = new();
        private readonly Mock<IOmnichannelActivityManager> _activityManager = new();
        private readonly Mock<IContentManager> _contentManager = new();
        private readonly Mock<IDisplayNameProvider> _displayNameProvider = new();
        private readonly DialerProfile _dialerProfile;

        public WorkspaceProbe(
            AgentPresenceStatus presence,
            Interaction active,
            Interaction recent = null,
            DateTime? presenceChangedUtc = null,
            string activitySource = ActivitySources.PowerDial,
            DialerProfile dialerProfile = null)
        {
            _dialerProfile = dialerProfile;

            var profile = new AgentProfile
            {
                ItemId = "agent-1",
                UserId = "user-1",
                DisplayName = "Agent One",
                PresenceStatus = presence,
                PresenceChangedUtc = presenceChangedUtc ?? ClockNow.AddMinutes(-1),
            };

            _agentManager
                .Setup(manager => manager.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(profile);

            _reservationManager
                .Setup(manager => manager.FindPendingByAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ActivityReservation)null);

            _interactionManager
                .Setup(manager => manager.FindActiveByAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(active);

            _interactionManager
                .Setup(manager => manager.GetRecentByAgentAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(recent is null ? [] : [recent]);

            var activity = new OmnichannelActivity
            {
                ItemId = "activity-1",
                Source = activitySource,
                Status = ActivityStatus.InProgress,
                AssignedToId = "user-1",
                PreferredDestination = "+17025550100",
            };

            _activityManager
                .Setup(manager => manager.GetByIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([activity]);

            _activityManager
                .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(activity);
        }

        public async Task<AgentWorkspaceStateViewModel> PollAsync()
        {
            var result = await AgentWorkspaceEndpoints.HandleStateAsync(
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
                CreateDialerProfileReader(_dialerProfile),
                new StubClock(ClockNow),
                Options.Create(new AgentAvailabilityOptions()),
                CreateLinkGenerator(),
                CreateHttpContext());

            var property = result.GetType().GetProperty("Value");

            Assert.NotNull(property);

            return Assert.IsType<AgentWorkspaceStateViewModel>(property.GetValue(result));
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
            => new Mock<UserManager<IUser>>(
                new Mock<IUserStore<IUser>>().Object,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null).Object;

        private static LinkGenerator CreateLinkGenerator()
            => new ServiceCollection()
                .AddLogging()
                .AddRouting()
                .BuildServiceProvider()
                .GetRequiredService<LinkGenerator>();

        // A dialer feature that is off reads no profile, like the platform's null-object default.
        private static IDialerProfileReader CreateDialerProfileReader(DialerProfile dialerProfile)
        {
            if (dialerProfile is null)
            {
                return new NullDialerProfileReader();
            }

            var reader = new Mock<IDialerProfileReader>();
            reader
                .Setup(r => r.FindByIdAsync(dialerProfile.ItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(dialerProfile);

            return reader.Object;
        }

        private static DefaultHttpContext CreateHttpContext()
            => new()
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test")),
                RequestServices = new ServiceCollection().BuildServiceProvider(),
            };
    }
}
