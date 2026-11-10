using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterActivityOwnershipAuthorizationHandlerTests
{
    private const string ActivityId = "activity-1";

    [Fact]
    public async Task HandleAsync_WhenWorkStateReservesActivityForUserBeforeActivityIsProjected_ShouldSucceed()
    {
        // Arrange: a preview offer reserved the activity, but the activity's own copy has no owner yet.
        var handler = CreateHandler(
            authorizeOwnActivity: true,
            new ContactCenterWorkState
            {
                ActivityItemId = ActivityId,
                ReservedById = "user-1",
            });
        var context = CreateContext(CreatePrincipal("user-1"), new OmnichannelActivity { ItemId = ActivityId });

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_WhenWorkStateAssignsActivityToUser_ShouldSucceed()
    {
        // Arrange
        var handler = CreateHandler(
            authorizeOwnActivity: true,
            new ContactCenterWorkState
            {
                ActivityItemId = ActivityId,
                AssignedToId = "user-1",
            });
        var context = CreateContext(CreatePrincipal("user-1"), new OmnichannelActivity { ItemId = ActivityId });

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_WhenResourceIsActivityContainer_ShouldSucceed()
    {
        // Arrange
        var handler = CreateHandler(
            authorizeOwnActivity: true,
            new ContactCenterWorkState
            {
                ActivityItemId = ActivityId,
                ReservedById = "user-1",
            });
        var container = new OmnichannelActivityContainer(
            new OmnichannelActivity { ItemId = ActivityId },
            new ContentTypeDefinition("Subject", "Subject"),
            contact: null,
            user: null);
        var context = CreateContext(CreatePrincipal("user-1"), container);

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_WhenWorkStateBelongsToAnotherUser_ShouldNotSucceed()
    {
        // Arrange
        var handler = CreateHandler(
            authorizeOwnActivity: true,
            new ContactCenterWorkState
            {
                ActivityItemId = ActivityId,
                ReservedById = "user-2",
                AssignedToId = "user-3",
            });
        var context = CreateContext(CreatePrincipal("user-1"), new OmnichannelActivity { ItemId = ActivityId });

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_WhenActivityWasNeverRouted_ShouldNotSucceed()
    {
        // Arrange
        var handler = CreateHandler(authorizeOwnActivity: true, workState: null);
        var context = CreateContext(CreatePrincipal("user-1"), new OmnichannelActivity { ItemId = ActivityId });

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_WhenUserLacksCompleteOwnPermission_ShouldNotSucceed()
    {
        // Arrange
        var handler = CreateHandler(
            authorizeOwnActivity: false,
            new ContactCenterWorkState
            {
                ActivityItemId = ActivityId,
                ReservedById = "user-1",
            });
        var context = CreateContext(CreatePrincipal("user-1"), new OmnichannelActivity { ItemId = ActivityId });

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_WhenPermissionIsNotCompleteActivity_ShouldNotLookUpWorkState()
    {
        // Arrange
        var workStateManager = new Mock<IContactCenterWorkStateManager>(MockBehavior.Strict);
        var handler = new ContactCenterActivityOwnershipAuthorizationHandler(
            workStateManager.Object,
            CreateServiceProvider(authorizeOwnActivity: true));
        var context = new AuthorizationHandlerContext(
            [new PermissionRequirement(OmnichannelConstants.Permissions.EditActivity)],
            CreatePrincipal("user-1"),
            new OmnichannelActivity { ItemId = ActivityId });

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
        workStateManager.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WhenRoutingReleasedTheWorkAfterTheAgentHandledTheCall_ShouldSucceed()
    {
        // Arrange: the agent talked to the caller, then the call's end let go of the work and cleared its owner, while
        // the disposition for that call is still owed (the agent saw "Complete activity" and was refused).
        var handler = CreateHandler(
            authorizeOwnActivity: true,
            ReleasedWorkState(),
            HandledCall(agentId: "agent-1"),
            new AgentProfile { ItemId = "agent-1", UserId = "user-1" });
        var context = CreateContext(CreatePrincipal("user-1"), new OmnichannelActivity { ItemId = ActivityId });

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_WhenAnotherAgentHandledTheReleasedWorksCall_ShouldNotSucceed()
    {
        // Arrange
        var handler = CreateHandler(
            authorizeOwnActivity: true,
            ReleasedWorkState(),
            HandledCall(agentId: "agent-2"),
            new AgentProfile { ItemId = "agent-1", UserId = "user-1" });
        var context = CreateContext(CreatePrincipal("user-1"), new OmnichannelActivity { ItemId = ActivityId });

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_WhenTheReleasedWorksCallWasNeverAnswered_ShouldNotSucceed()
    {
        // Arrange: the offer rang the agent, but nobody took the call.
        var unanswered = new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = ActivityId,
            AgentId = "agent-1",
        }.RestorePersistedStatus(InteractionStatus.Ended);

        var handler = CreateHandler(
            authorizeOwnActivity: true,
            ReleasedWorkState(),
            unanswered,
            new AgentProfile { ItemId = "agent-1", UserId = "user-1" });
        var context = CreateContext(CreatePrincipal("user-1"), new OmnichannelActivity { ItemId = ActivityId });

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_WhenTheActivityIsAlreadyCompleted_ShouldNotLookUpTheCall()
    {
        // Arrange
        var interactionManager = new Mock<IInteractionManager>(MockBehavior.Strict);
        var handler = CreateHandler(
            authorizeOwnActivity: true,
            ReleasedWorkState(),
            interactionManager.Object,
            new Mock<IAgentProfileManager>(MockBehavior.Strict).Object);
        var context = CreateContext(
            CreatePrincipal("user-1"),
            new OmnichannelActivity { ItemId = ActivityId, Status = ActivityStatus.Completed });

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
        interactionManager.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HandleAsync_WhenAnotherUserHoldsTheWorkNow_ShouldNotLookUpTheCall()
    {
        // Arrange: the record went back to the queue and another agent has it, so the earlier call does not count.
        var interactionManager = new Mock<IInteractionManager>(MockBehavior.Strict);
        var handler = CreateHandler(
            authorizeOwnActivity: true,
            new ContactCenterWorkState
            {
                ActivityItemId = ActivityId,
                AssignedToId = "user-2",
            },
            interactionManager.Object,
            new Mock<IAgentProfileManager>(MockBehavior.Strict).Object);
        var context = CreateContext(CreatePrincipal("user-1"), new OmnichannelActivity { ItemId = ActivityId });

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
        interactionManager.VerifyNoOtherCalls();
    }

    private static ContactCenterWorkState ReleasedWorkState()
        => new()
        {
            ActivityItemId = ActivityId,
        };

    private static Interaction HandledCall(string agentId)
    {
        return new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = ActivityId,
            AgentId = agentId,
            AnsweredUtc = new DateTime(2026, 11, 10, 19, 27, 0, DateTimeKind.Utc),
            EndedUtc = new DateTime(2026, 11, 10, 19, 38, 0, DateTimeKind.Utc),
        }.RestorePersistedStatus(InteractionStatus.Ended);
    }

    private static ContactCenterActivityOwnershipAuthorizationHandler CreateHandler(
        bool authorizeOwnActivity,
        ContactCenterWorkState workState,
        Interaction lastCall,
        AgentProfile agent)
    {
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByActivityIdAsync(ActivityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lastCall);

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByUserIdAsync(agent.UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(agent);

        return CreateHandler(authorizeOwnActivity, workState, interactionManager.Object, agentManager.Object);
    }

    private static ContactCenterActivityOwnershipAuthorizationHandler CreateHandler(
        bool authorizeOwnActivity,
        ContactCenterWorkState workState,
        IInteractionManager interactionManager = null,
        IAgentProfileManager agentManager = null)
    {
        var workStateManager = new Mock<IContactCenterWorkStateManager>();
        workStateManager
            .Setup(manager => manager.FindByActivityIdAsync(ActivityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workState);

        return new ContactCenterActivityOwnershipAuthorizationHandler(
            workStateManager.Object,
            CreateServiceProvider(authorizeOwnActivity, interactionManager, agentManager));
    }

    private static ServiceProvider CreateServiceProvider(
        bool authorizeOwnActivity,
        IInteractionManager interactionManager = null,
        IAgentProfileManager agentManager = null)
    {
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(authorizeOwnActivity
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());

        var services = new ServiceCollection()
            .AddSingleton(authorizationService.Object);

        if (interactionManager is not null)
        {
            services.AddSingleton(interactionManager);
        }

        if (agentManager is not null)
        {
            services.AddSingleton(agentManager);
        }

        return services.BuildServiceProvider();
    }

    private static AuthorizationHandlerContext CreateContext(ClaimsPrincipal user, object resource)
    {
        return new AuthorizationHandlerContext(
            [new PermissionRequirement(OmnichannelConstants.Permissions.CompleteActivity)],
            user,
            resource);
    }

    private static ClaimsPrincipal CreatePrincipal(string userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
        ],
        authenticationType: "Test"));
    }
}
