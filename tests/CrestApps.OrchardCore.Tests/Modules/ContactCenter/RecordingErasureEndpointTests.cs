using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class RecordingErasureEndpointTests
{
    [Fact]
    public async Task HandleAsync_RecordsTheErasureAsTheSupervisorsAct()
    {
        // Arrange
        // Erasing a recording on request takes the interaction-management permission, which is a supervisor's; the
        // audit trail named the user but left the kind of actor unspecified.
        var interaction = new Interaction
        {
            ItemId = "int1",
            RecordingReference = "rec-1",
            TechnicalMetadata = new Dictionary<string, object>(),
        };

        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByIdAsync("int1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var published = new List<InteractionEvent>();
        var publisher = new Mock<IContactCenterEventPublisher>();
        publisher
            .Setup(value => value.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) => published.Add(interactionEvent))
            .Returns(Task.CompletedTask);

        var governance = new RecordingAccessGovernanceService(
            interactionManager.Object,
            new Mock<ICallSessionManager>().Object,
            publisher.Object,
            new Mock<IClock>().Object);

        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "supervisor-1")], "Test")),
        };

        // Act
        await RecordingErasureEndpoint.HandleAsync(
            new RecordingErasureRequest
            {
                InteractionId = "int1",
                Reason = "gdpr-subject-request",
            },
            authorizationService.Object,
            new Mock<IAntiforgery>().Object,
            governance,
            httpContext);

        // Assert
        var erased = Assert.Single(published, e => e.EventType == ContactCenterConstants.Events.RecordingErased);
        Assert.Equal(ContactCenterActorType.Supervisor, erased.ActorType);
        Assert.Equal("supervisor-1", erased.ActorId);
    }
}
