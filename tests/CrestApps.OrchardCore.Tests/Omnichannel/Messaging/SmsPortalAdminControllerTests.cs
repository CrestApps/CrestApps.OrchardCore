using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Controllers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Security;
using OrchardCore.Users;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

public sealed class SmsPortalAdminControllerTests
{
    private const string ConversationId = "conv-1";

    [Fact]
    public async Task Conversation_WhenTheThreadIsNotTheCallers_ReturnsForbid()
    {
        var conversation = CreateForeignConversation();
        var controller = CreateController(conversation, allowConversation: false);

        var result = await controller.Conversation(ConversationId, show: null, channel: null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ThreadMessages_WhenTheThreadIsNotTheCallers_ReturnsForbid()
    {
        var conversation = CreateForeignConversation();
        var controller = CreateController(conversation, allowConversation: false);

        var result = await controller.ThreadMessages(ConversationId, 0);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task Claim_WhenTheThreadIsNotTheCallers_ReturnsForbid_AndNeverClaims()
    {
        var conversation = CreateForeignConversation();
        var conversationService = new Mock<IMessagingConversationService>();
        var controller = CreateController(conversation, allowConversation: false, conversationService: conversationService);

        var result = await controller.Claim(ConversationId);

        Assert.IsType<ForbidResult>(result);
        conversationService.Verify(
            service => service.ClaimAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Send_WhenTheThreadIsNotTheCallers_ReturnsForbid_AndNeverSends()
    {
        var conversation = CreateForeignConversation();
        var conversationService = new Mock<IMessagingConversationService>();
        var controller = CreateController(conversation, allowConversation: false, conversationService: conversationService);

        var result = await controller.Send(ConversationId, "hello", subject: null);

        Assert.IsType<ForbidResult>(result);
        conversationService.Verify(
            service => service.SendAsync(It.IsAny<MessagingSendRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SetStatus_WhenTheThreadIsNotTheCallers_ReturnsForbid_AndNeverUpdates()
    {
        var conversation = CreateForeignConversation();
        var conversationService = new Mock<IMessagingConversationService>();
        var controller = CreateController(conversation, allowConversation: false, conversationService: conversationService);

        var result = await controller.SetStatus(ConversationId, ConversationStatus.Closed);

        Assert.IsType<ForbidResult>(result);
        conversationService.Verify(
            service => service.SetStatusAsync(It.IsAny<string>(), It.IsAny<ConversationStatus>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SetStatus_WhenTheThreadIsTheCallers_UpdatesAndRedirects()
    {
        var conversation = CreateForeignConversation();
        var conversationService = new Mock<IMessagingConversationService>();

        conversationService
            .Setup(service => service.SetStatusAsync(It.IsAny<string>(), It.IsAny<ConversationStatus>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessagingSendResult { Succeeded = true });

        var controller = CreateController(conversation, allowConversation: true, conversationService: conversationService);

        var result = await controller.SetStatus(ConversationId, ConversationStatus.Closed);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Conversation), redirect.ActionName);
        conversationService.Verify(
            service => service.SetStatusAsync(ConversationId, ConversationStatus.Closed, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Claim_WhenTheThreadDoesNotExist_ReturnsNotFound()
    {
        var controller = CreateController(conversation: null, allowConversation: true);

        var result = await controller.Claim(ConversationId);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Transfer_WhenTheCallerMayNotTransferIt_ReturnsForbid_AndNeverTransfers()
    {
        var conversation = CreateForeignConversation();
        var transferService = new Mock<IMessagingConversationTransferService>();
        var controller = CreateController(
            conversation,
            allowConversation: true,
            transferService: transferService,
            deniedOperations: new HashSet<ConversationOperation> { ConversationOperation.Transfer });

        var result = await controller.Transfer(ConversationId, ConversationRouteTargetType.Agent, "agent-2", targetQueueId: null, note: null);

        Assert.IsType<ForbidResult>(result);
        transferService.Verify(
            service => service.TransferAsync(It.IsAny<MessagingTransferRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Transfer_ToSomeoneWhoCannotUseMessaging_IsRefused_AndNeverTransfers()
    {
        // No user stands behind the target's agent profile, so they could never open what they were sent.
        var conversation = CreateForeignConversation();
        var transferService = new Mock<IMessagingConversationTransferService>();
        var controller = CreateController(conversation, allowConversation: true, transferService: transferService, targetUser: null);

        var result = await controller.Transfer(ConversationId, ConversationRouteTargetType.Agent, "agent-2", targetQueueId: null, note: null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Conversation), redirect.ActionName);
        transferService.Verify(
            service => service.TransferAsync(It.IsAny<MessagingTransferRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Transfer_ToAPerson_PassesTheTargetNoteAndCaller_AndReturnsTheSenderToTheirList()
    {
        var conversation = CreateForeignConversation();
        var transferService = new Mock<IMessagingConversationTransferService>();

        transferService
            .Setup(service => service.TransferAsync(It.IsAny<MessagingTransferRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessagingTransferResult
            {
                Succeeded = true,
                Conversation = conversation,
                Event = new MessagingConversationEvent { ToName = "Bea Recipient" },
            });

        // After the transfer the sender may no longer open the conversation.
        var controller = CreateController(
            conversation,
            allowConversation: true,
            transferService: transferService,
            deniedOperations: new HashSet<ConversationOperation> { ConversationOperation.View },
            targetUser: Mock.Of<IUser>());

        var result = await controller.Transfer(ConversationId, ConversationRouteTargetType.Agent, "agent-2", targetQueueId: "ignored-queue", note: "Invoice question");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Index), redirect.ActionName);
        transferService.Verify(
            service => service.TransferAsync(
                It.Is<MessagingTransferRequest>(request =>
                    request.ConversationId == ConversationId &&
                    request.TargetType == ConversationRouteTargetType.Agent &&
                    request.TargetId == "agent-2" &&
                    request.Note == "Invoice question" &&
                    request.ActingAgentId == "agent-1" &&
                    request.Principal != null),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Transfer_ToATeam_UsesTheQueueTarget()
    {
        var conversation = CreateForeignConversation();
        var transferService = new Mock<IMessagingConversationTransferService>();

        transferService
            .Setup(service => service.TransferAsync(It.IsAny<MessagingTransferRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessagingTransferResult { Succeeded = true, Conversation = conversation });

        var controller = CreateController(conversation, allowConversation: true, transferService: transferService);

        var result = await controller.Transfer(ConversationId, ConversationRouteTargetType.Queue, "ignored-agent", targetQueueId: "queue-1", note: null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Conversation), redirect.ActionName);
        transferService.Verify(
            service => service.TransferAsync(
                It.Is<MessagingTransferRequest>(request => request.TargetType == ConversationRouteTargetType.Queue && request.TargetId == "queue-1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TransferAgents_WhenTheCallerMayNotTransferIt_ReturnsForbid()
    {
        var conversation = CreateForeignConversation();
        var controller = CreateController(
            conversation,
            allowConversation: true,
            deniedOperations: new HashSet<ConversationOperation> { ConversationOperation.Transfer });

        var result = await controller.TransferAgents(ConversationId, query: null);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task TransferQueues_WhenTheThreadDoesNotExist_ReturnsNotFound()
    {
        var controller = CreateController(conversation: null, allowConversation: true);

        var result = await controller.TransferQueues(ConversationId, query: null);

        Assert.IsType<NotFoundResult>(result);
    }

    private static MessagingConversation CreateForeignConversation()
        => new()
        {
            Channel = "SMS",
            ItemId = ConversationId,
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            OwnerType = ConversationOwnerType.Personal,
            OwnerId = "agent-other",
            AssignedAgentId = "agent-other",
            AssignmentStatus = ConversationAssignmentStatus.Assigned,
        };

    private static AdminController CreateController(
        MessagingConversation conversation,
        bool allowConversation,
        Mock<IMessagingConversationService> conversationService = null,
        Mock<IMessagingConversationTransferService> transferService = null,
        ISet<ConversationOperation> deniedOperations = null,
        IUser targetUser = null)
    {
        var conversationStore = new Mock<IMessagingConversationStore>();

        conversationStore
            .Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        var agentProfileManager = new Mock<IAgentProfileManager>();

        agentProfileManager
            .Setup(manager => manager.FindByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "user-1" });

        var clock = new Mock<IClock>();
        clock.SetupGet(instance => instance.UtcNow).Returns(DateTime.UtcNow);

        var channels = MessagingTestChannels.Resolver(MessagingTestChannels.AcceptingDispatcher().Object);
        var authorizationService = new ConversationAuthorizationService(allowConversation, deniedOperations);

        agentProfileManager
            .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string agentId, CancellationToken _) => new AgentProfile { ItemId = agentId, UserId = "user-of-" + agentId });

        var userManager = MockUserManager();
        userManager
            .Setup(manager => manager.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(targetUser);

        var principalFactory = new Mock<IUserClaimsPrincipalFactory<IUser>>();
        principalFactory
            .Setup(factory => factory.CreateAsync(It.IsAny<IUser>()))
            .ReturnsAsync(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "target-user")], "Test")));

        var transferTargets = new MessagingTransferTargets(
            agentProfileManager.Object,
            [],
            userManager.Object,
            principalFactory.Object,
            authorizationService,
            Mock.Of<IDisplayNameProvider>());

        var workspaceBuilder = new MessagingWorkspaceBuilder(
            conversationStore.Object,
            channels,
            Mock.Of<IOmnichannelChannelEndpointManager>(),
            Mock.Of<IMessageTemplateManager>(),
            agentProfileManager.Object,
            Mock.Of<IMessagingAvailabilityService>(),
            Mock.Of<IMessagingAgentNameProvider>(),
            [],
            Mock.Of<IContentManager>(),
            authorizationService,
            new MessagingQuietHoursGuard(
                Mock.Of<IBusinessHoursGate>(),
                Mock.Of<IMessagingQueuePolicyReader>(),
                Mock.Of<IMessagingContactTimeZoneResolver>(),
                clock.Object),
            Mock.Of<IDisplayManager<MessagingConversation>>(),
            Mock.Of<IUpdateModelAccessor>(),
            Mock.Of<YesSqlSession>(),
            clock.Object,
            new OptionsWrapper<MessagingWorkspaceOptions>(new MessagingWorkspaceOptions()),
            new NullStringLocalizer<MessagingWorkspaceBuilder>());

        var controller = new AdminController(
            conversationStore.Object,
            (conversationService ?? new Mock<IMessagingConversationService>()).Object,
            (transferService ?? new Mock<IMessagingConversationTransferService>()).Object,
            transferTargets,
            Mock.Of<IMessagingBroadcastManager>(),
            channels,
            Mock.Of<IOmnichannelChannelEndpointManager>(),
            Mock.Of<IMessagingAvailabilityService>(),
            workspaceBuilder,
            new MessagingContactSearch(channels, Mock.Of<IOmnichannelContactTypeProvider>(), Mock.Of<IContentManager>(), Mock.Of<YesSqlSession>()),
            authorizationService,
            Mock.Of<INotifier>(),
            new NullHtmlLocalizer(),
            new NullStringLocalizer<AdminController>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test")),
            },
        };

        return controller;
    }

    private static Mock<UserManager<IUser>> MockUserManager()
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
            null);
    }

    // Grants the portal permission, and grants (or denies) the per-conversation rule the way the
    // MessagingConversationAuthorizationHandler does at runtime.
    private sealed class ConversationAuthorizationService : IAuthorizationService
    {
        private readonly bool _allowConversation;
        private readonly ISet<ConversationOperation> _deniedOperations;

        public ConversationAuthorizationService(bool allowConversation, ISet<ConversationOperation> deniedOperations = null)
        {
            _allowConversation = allowConversation;
            _deniedOperations = deniedOperations ?? new HashSet<ConversationOperation>();
        }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            IEnumerable<IAuthorizationRequirement> requirements)
        {
            if (resource is ConversationAuthorizationResource conversationResource &&
                (!_allowConversation || _deniedOperations.Contains(conversationResource.Operation)))
            {
                return Task.FromResult(AuthorizationResult.Failed());
            }

            var isSupervisorCheck = requirements
                .OfType<PermissionRequirement>()
                .Any(requirement => requirement.Permission.Name == MessagingPermissions.ViewAllConversations.Name);

            return Task.FromResult(isSupervisorCheck
                ? AuthorizationResult.Failed()
                : AuthorizationResult.Success());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Success());
    }

    private sealed class NullStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private sealed class NullHtmlLocalizer : IHtmlLocalizer<AdminController>
    {
        public LocalizedHtmlString this[string name] => new(name, name);

        public LocalizedHtmlString this[string name, params object[] arguments] => new(name, string.Format(name, arguments));

        public LocalizedString GetString(string name) => new(name, name);

        public LocalizedString GetString(string name, params object[] arguments) => new(name, string.Format(name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
