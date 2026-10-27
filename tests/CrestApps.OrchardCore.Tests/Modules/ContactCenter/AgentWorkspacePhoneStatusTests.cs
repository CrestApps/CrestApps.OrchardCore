using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
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

public sealed class AgentWorkspacePhoneStatusTests
{
    private static readonly DateTime ClockNow = new(2026, 10, 27, 17, 32, 40, DateTimeKind.Utc);

    [Fact]
    public async Task HandleStateAsync_SignedInAgentWithNoPhoneOpen_SaysNoPhoneIsOpen()
    {
        // Arrange
        var probe = new PhoneStatusProbe(signedIn: true, new FixedPhoneStatus(false));

        // Act
        var state = await probe.PollAsync();

        // Assert
        Assert.True(state.IsSignedIn);
        Assert.False(state.PhoneOpen);
    }

    [Fact]
    public async Task HandleStateAsync_AnyProviderSeesAPhoneOpen_SaysItIsOpen()
    {
        // Arrange
        var probe = new PhoneStatusProbe(signedIn: true, new FixedPhoneStatus(false), new FixedPhoneStatus(true));

        // Act
        var state = await probe.PollAsync();

        // Assert
        Assert.True(state.PhoneOpen);
    }

    [Fact]
    public async Task HandleStateAsync_NoProviderCanTell_LeavesItUnknown()
    {
        // Arrange
        var probe = new PhoneStatusProbe(signedIn: true, new FixedPhoneStatus(null));

        // Act
        var state = await probe.PollAsync();

        // Assert
        Assert.Null(state.PhoneOpen);
    }

    [Fact]
    public async Task HandleStateAsync_AgentNotSignedIn_DoesNotAskAboutThePhone()
    {
        // Arrange
        var provider = new FixedPhoneStatus(false);
        var probe = new PhoneStatusProbe(signedIn: false, provider);

        // Act
        var state = await probe.PollAsync();

        // Assert
        Assert.False(state.IsSignedIn);
        Assert.Null(state.PhoneOpen);
        Assert.Equal(0, provider.Calls);
    }

    private sealed class FixedPhoneStatus : IAgentPhoneStatusProvider
    {
        private readonly bool? _status;

        public FixedPhoneStatus(bool? status)
        {
            _status = status;
        }

        public int Calls { get; private set; }

        public Task<bool?> HasOpenPhoneAsync(string userId, CancellationToken cancellationToken = default)
        {
            Assert.Equal("user-1", userId);
            Calls++;

            return Task.FromResult(_status);
        }
    }

    private sealed class PhoneStatusProbe
    {
        private readonly Mock<IAgentProfileManager> _agentManager = new();
        private readonly Mock<IInteractionManager> _interactionManager = new();
        private readonly IAgentPhoneStatusProvider[] _providers;

        public PhoneStatusProbe(bool signedIn, params IAgentPhoneStatusProvider[] providers)
        {
            _providers = providers;

            _agentManager
                .Setup(manager => manager.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentProfile
                {
                    ItemId = "agent-1",
                    UserId = "user-1",
                    DisplayName = "Agent One",
                    PresenceStatus = AgentPresenceStatus.Available,
                    QueueIds = signedIn ? ["q1"] : [],
                });

            _interactionManager
                .Setup(manager => manager.GetRecentByAgentAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
        }

        public async Task<AgentWorkspaceStateViewModel> PollAsync()
        {
            var services = new ServiceCollection();

            foreach (var provider in _providers)
            {
                services.AddSingleton(provider);
            }

            var result = await AgentWorkspaceEndpoints.HandleStateAsync(
                new AlwaysAllowAuthorizationService(),
                _agentManager.Object,
                Mock.Of<IActivityReservationManager>(),
                Mock.Of<IActivityQueueManager>(),
                Mock.Of<IQueueItemManager>(),
                _interactionManager.Object,
                Mock.Of<IOmnichannelActivityManager>(),
                Mock.Of<IContentManager>(),
                new Mock<UserManager<IUser>>(new Mock<IUserStore<IUser>>().Object, null, null, null, null, null, null, null, null).Object,
                Mock.Of<IDisplayNameProvider>(),
                Mock.Of<IContactCenterVoiceProviderResolver>(),
                new NullDialerProfileReader(),
                new StubClock(ClockNow),
                Options.Create(new AgentAvailabilityOptions()),
                new ServiceCollection().AddLogging().AddRouting().BuildServiceProvider().GetRequiredService<LinkGenerator>(),
                new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test")),
                    RequestServices = services.BuildServiceProvider(),
                });

            var property = result.GetType().GetProperty("Value");

            Assert.NotNull(property);

            return Assert.IsType<AgentWorkspaceStateViewModel>(property.GetValue(result));
        }

        private sealed class AlwaysAllowAuthorizationService : IAuthorizationService
        {
            public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
                => Task.FromResult(AuthorizationResult.Success());

            public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
                => Task.FromResult(AuthorizationResult.Success());
        }
    }
}
