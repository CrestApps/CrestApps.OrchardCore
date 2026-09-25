using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class SoftPhoneTransferEndpointTests
{
    [Fact]
    public async Task Targets_WhenNobodyIsSignedIn_AnswersUnauthorizedWithAProblemBody_NotARedirect()
    {
        var result = await AgentSoftPhoneTransferEndpoints.HandleTargetsAsync(
            "interaction-1",
            new AllowAll(),
            FakeCallControlAuthorizationService.Resolving("call-1"),
            InteractionManager(),
            Directory().Object,
            new DefaultHttpContext());

        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Targets_WhenTheUserIsNotAnAgent_AnswersForbidden()
    {
        var result = await AgentSoftPhoneTransferEndpoints.HandleTargetsAsync(
            "interaction-1",
            new DenyAll(),
            FakeCallControlAuthorizationService.Resolving("call-1"),
            InteractionManager(),
            Directory().Object,
            SignedIn());

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Targets_WhenTheCallIsNotTheAgents_AnswersNotFound()
    {
        var result = await AgentSoftPhoneTransferEndpoints.HandleTargetsAsync(
            "interaction-1",
            new AllowAll(),
            FakeCallControlAuthorizationService.Denying(),
            InteractionManager(),
            Directory().Object,
            SignedIn());

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    [Fact]
    public async Task Targets_ForTheAgentsOwnCall_ReturnsTheDirectoryForTheCallsProvider()
    {
        var expected = new SoftPhoneTransferDirectory { SupportsConsult = true };
        var directory = Directory(expected);

        var result = await AgentSoftPhoneTransferEndpoints.HandleTargetsAsync(
            "interaction-1",
            new AllowAll(),
            FakeCallControlAuthorizationService.Resolving("call-1"),
            InteractionManager(),
            directory.Object,
            SignedIn());

        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Same(expected, Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.Equal(("user-1", "Telnyx"), Assert.Single(directory.Calls));
    }

    [Fact]
    public async Task Transfer_SendsTheAgentsBlindTransferThroughTheContactCenter()
    {
        var transferService = new Mock<IContactCenterTransferService>();
        transferService
            .Setup(service => service.TransferAsync(It.IsAny<TransferRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransferResult.Success("The call is ringing for Bea."));

        var result = await AgentSoftPhoneTransferEndpoints.HandleTransferAsync(
            new SoftPhoneTransferBody { InteractionId = "interaction-1", TargetType = "agent", TargetId = "agent-b" },
            new AllowAll(),
            Antiforgery(valid: true),
            transferService.Object,
            ExtensionTargets(),
            SignedIn());

        var body = Assert.IsType<SoftPhoneTransferResponse>(Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.True(body.Succeeded);
        Assert.Equal("The call is ringing for Bea.", body.Message);
        transferService.Verify(service => service.TransferAsync(
            It.Is<TransferRequest>(request =>
                request.InteractionId == "interaction-1" &&
                request.InitiatedByUserId == "user-1" &&
                request.Type == InteractionTransferType.Blind &&
                request.TargetType == InteractionTransferTargetType.Agent &&
                request.TargetId == "agent-b"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Transfer_WhenTheTransferIsRefused_ReportsWhyInTheBody()
    {
        var transferService = new Mock<IContactCenterTransferService>();
        transferService
            .Setup(service => service.TransferAsync(It.IsAny<TransferRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransferResult.Failure("Bea is not available to take the call."));

        var result = await AgentSoftPhoneTransferEndpoints.HandleTransferAsync(
            new SoftPhoneTransferBody { InteractionId = "interaction-1", TargetType = "agent", TargetId = "agent-b" },
            new AllowAll(),
            Antiforgery(valid: true),
            transferService.Object,
            ExtensionTargets(),
            SignedIn());

        var body = Assert.IsType<SoftPhoneTransferResponse>(Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.False(body.Succeeded);
        Assert.Equal("Bea is not available to take the call.", body.Error);
    }

    [Fact]
    public async Task Transfer_WithoutTheAntiforgeryToken_IsRefusedBeforeAnythingMoves()
    {
        var transferService = new Mock<IContactCenterTransferService>();

        var result = await AgentSoftPhoneTransferEndpoints.HandleTransferAsync(
            new SoftPhoneTransferBody { InteractionId = "interaction-1", TargetType = "queue", TargetId = "queue-1" },
            new AllowAll(),
            Antiforgery(valid: false),
            transferService.Object,
            ExtensionTargets(),
            SignedIn());

        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        transferService.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("agent", InteractionTransferTargetType.Agent)]
    [InlineData("queue", InteractionTransferTargetType.Queue)]
    [InlineData("external", InteractionTransferTargetType.External)]
    public async Task Transfer_ReadsTheDestinationKind(string targetType, InteractionTransferTargetType expected)
    {
        var transferService = new Mock<IContactCenterTransferService>();
        transferService
            .Setup(service => service.TransferAsync(It.IsAny<TransferRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransferResult.Success());

        await AgentSoftPhoneTransferEndpoints.HandleTransferAsync(
            new SoftPhoneTransferBody { InteractionId = "interaction-1", TargetType = targetType, TargetId = "x" },
            new AllowAll(),
            Antiforgery(valid: true),
            transferService.Object,
            ExtensionTargets(),
            SignedIn());

        transferService.Verify(service => service.TransferAsync(It.Is<TransferRequest>(request => request.TargetType == expected), It.IsAny<CancellationToken>()), Times.Once);
    }

    // Bug: an extension typed into the transfer panel was refused as an incomplete phone number. It now arrives as an
    // extension, and is routed to the agent it rings exactly as picking that agent would route it.
    [Fact]
    public async Task Transfer_ToAnExtension_RoutesToTheAgentItRings()
    {
        var transferService = new Mock<IContactCenterTransferService>();
        transferService
            .Setup(service => service.TransferAsync(It.IsAny<TransferRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TransferResult.Success("The call is ringing for Bea."));

        var result = await AgentSoftPhoneTransferEndpoints.HandleTransferAsync(
            new SoftPhoneTransferBody { InteractionId = "interaction-1", TargetType = "extension", TargetId = "2" },
            new AllowAll(),
            Antiforgery(valid: true),
            transferService.Object,
            ExtensionTargets(("2", "agent-b")),
            SignedIn());

        Assert.True(Assert.IsType<SoftPhoneTransferResponse>(Assert.IsAssignableFrom<IValueHttpResult>(result).Value).Succeeded);
        transferService.Verify(service => service.TransferAsync(
            It.Is<TransferRequest>(request => request.TargetType == InteractionTransferTargetType.Agent && request.TargetId == "agent-b"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Transfer_ToAnExtensionThatIsNotAnAgent_IsRefusedWithWhy_AndNothingMoves()
    {
        var transferService = new Mock<IContactCenterTransferService>();

        var result = await AgentSoftPhoneTransferEndpoints.HandleTransferAsync(
            new SoftPhoneTransferBody { InteractionId = "interaction-1", TargetType = "extension", TargetId = "7" },
            new AllowAll(),
            Antiforgery(valid: true),
            transferService.Object,
            ExtensionTargets(),
            SignedIn());

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Contains("7", problem.ProblemDetails.Detail);
        transferService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ConsultStart_ToAnExtension_ConsultsTheAgentItRings()
    {
        var warm = new Mock<IWarmTransferService>();
        warm.Setup(service => service.StartAsync(It.IsAny<WarmTransferRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WarmTransferResult { Succeeded = true, ConsultId = "consult-1", Status = ConsultCallStatus.Initiated, TargetType = InteractionTransferTargetType.Agent, TargetId = "agent-b" });

        await AgentSoftPhoneTransferEndpoints.HandleConsultStartAsync(
            new SoftPhoneTransferBody { InteractionId = "interaction-1", TargetType = "extension", TargetId = "2" },
            new AllowAll(),
            Antiforgery(valid: true),
            warm.Object,
            ExtensionTargets(("2", "agent-b")),
            SignedIn());

        warm.Verify(service => service.StartAsync(
            It.Is<WarmTransferRequest>(request => request.TargetType == InteractionTransferTargetType.Agent && request.TargetId == "agent-b"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsultStart_StartsAWarmTransferAndReturnsTheConsult()
    {
        var warm = new Mock<IWarmTransferService>();
        warm.Setup(service => service.StartAsync(It.IsAny<WarmTransferRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WarmTransferResult { Succeeded = true, ConsultId = "consult-1", Status = ConsultCallStatus.Initiated, TargetType = InteractionTransferTargetType.Agent, TargetId = "agent-b" });

        var result = await AgentSoftPhoneTransferEndpoints.HandleConsultStartAsync(
            new SoftPhoneTransferBody { InteractionId = "interaction-1", TargetType = "agent", TargetId = "agent-b" },
            new AllowAll(),
            Antiforgery(valid: true),
            warm.Object,
            ExtensionTargets(),
            SignedIn());

        var body = Assert.IsType<SoftPhoneConsultResponse>(Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
        Assert.True(body.Succeeded);
        Assert.Equal("consult-1", body.Consult.Id);
        Assert.Equal("ringing", body.Consult.Status);
        Assert.True(body.Consult.Live);
        warm.Verify(service => service.StartAsync(
            It.Is<WarmTransferRequest>(request => request.UserId == "user-1" && request.TargetId == "agent-b" && request.TargetType == InteractionTransferTargetType.Agent),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsultComplete_AndCancel_ActOnTheNamedConsult()
    {
        var warm = new Mock<IWarmTransferService>();
        warm.Setup(service => service.CompleteAsync(It.IsAny<WarmTransferCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WarmTransferResult { Succeeded = true, ConsultId = "consult-1", Status = ConsultCallStatus.Completed });
        warm.Setup(service => service.CancelAsync(It.IsAny<WarmTransferCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WarmTransferResult { Succeeded = true, ConsultId = "consult-1", Status = ConsultCallStatus.Cancelled });

        var completed = await AgentSoftPhoneTransferEndpoints.HandleConsultCompleteAsync(
            new SoftPhoneConsultBody { InteractionId = "interaction-1", ConsultId = "consult-1" },
            new AllowAll(),
            Antiforgery(valid: true),
            warm.Object,
            SignedIn());
        var cancelled = await AgentSoftPhoneTransferEndpoints.HandleConsultCancelAsync(
            new SoftPhoneConsultBody { InteractionId = "interaction-1", ConsultId = "consult-1" },
            new AllowAll(),
            Antiforgery(valid: true),
            warm.Object,
            SignedIn());

        Assert.Equal("completed", Assert.IsType<SoftPhoneConsultResponse>(Assert.IsAssignableFrom<IValueHttpResult>(completed).Value).Consult.Status);
        Assert.Equal("cancelled", Assert.IsType<SoftPhoneConsultResponse>(Assert.IsAssignableFrom<IValueHttpResult>(cancelled).Value).Consult.Status);
        warm.Verify(service => service.CompleteAsync(It.Is<WarmTransferCommand>(command => command.ConsultId == "consult-1" && command.UserId == "user-1"), It.IsAny<CancellationToken>()), Times.Once);
        warm.Verify(service => service.CancelAsync(It.Is<WarmTransferCommand>(command => command.ConsultId == "consult-1" && command.UserId == "user-1"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsultStatus_WhenNobodyIsSignedIn_AnswersUnauthorized()
    {
        var result = await AgentSoftPhoneTransferEndpoints.HandleConsultStatusAsync(
            "interaction-1",
            "consult-1",
            new AllowAll(),
            new Mock<IWarmTransferService>().Object,
            new DefaultHttpContext());

        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsType<ProblemHttpResult>(result).StatusCode);
    }

    private static FakeExtensionTargets ExtensionTargets(params (string Extension, string AgentId)[] agents)
        => new(agents.ToDictionary(agent => agent.Extension, agent => agent.AgentId));

    private sealed class FakeExtensionTargets : ISoftPhoneExtensionTransferTargetResolver
    {
        private readonly Dictionary<string, string> _agents;

        public FakeExtensionTargets(Dictionary<string, string> agents)
        {
            _agents = agents;
        }

        public Task<SoftPhoneExtensionTransferTarget> ResolveAsync(string extension, CancellationToken cancellationToken = default)
            => Task.FromResult(_agents.TryGetValue(extension ?? string.Empty, out var agentId)
                ? SoftPhoneExtensionTransferTarget.Agent(agentId)
                : SoftPhoneExtensionTransferTarget.Refused($"Extension {extension} was not found."));
    }

    private static FakeDirectory Directory(SoftPhoneTransferDirectory directory = null)
        => new(directory ?? new SoftPhoneTransferDirectory());

    private sealed class FakeDirectory : IContactCenterTransferDirectoryService
    {
        private readonly SoftPhoneTransferDirectory _directory;

        public FakeDirectory(SoftPhoneTransferDirectory directory)
        {
            _directory = directory;
        }

        public FakeDirectory Object => this;

        public List<(string UserId, string ProviderName)> Calls { get; } = [];

        public Task<SoftPhoneTransferDirectory> GetAsync(string userId, ClaimsPrincipal principal, string providerName, CancellationToken cancellationToken = default)
        {
            Calls.Add((userId, providerName));

            return Task.FromResult(_directory);
        }
    }

    private static IInteractionManager InteractionManager()
    {
        var manager = new Mock<IInteractionManager>();
        manager
            .Setup(value => value.FindByIdAsync("interaction-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction { ItemId = "interaction-1", ProviderName = "Telnyx" });

        return manager.Object;
    }

    private static IAntiforgery Antiforgery(bool valid)
    {
        var antiforgery = new Mock<IAntiforgery>();
        antiforgery
            .Setup(service => service.ValidateRequestAsync(It.IsAny<HttpContext>()))
            .Returns(valid ? Task.CompletedTask : Task.FromException(new AntiforgeryValidationException("invalid")));

        return antiforgery.Object;
    }

    private static DefaultHttpContext SignedIn()
        => new()
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test")),
        };

    private sealed class AllowAll : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(AuthorizationResult.Success());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Success());
    }

    private sealed class DenyAll : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(AuthorizationResult.Failed());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());
    }
}
