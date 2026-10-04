using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Controllers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Attachments;
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
using Microsoft.Extensions.Logging.Abstractions;
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

    // The workspace calls this when the agent comes back to a thread whose new messages arrived while the page was in
    // the background: they were put on screen then, but left unread so the menu could count them.
    [Fact]
    public async Task MarkRead_ReadsAnUnreadConversation()
    {
        var conversation = CreateForeignConversation();
        conversation.IsRead = false;
        conversation.UnreadCount = 2;
        var store = new Mock<IMessagingConversationStore>();
        var controller = CreateController(conversation, allowConversation: true, store: store);

        var result = await controller.MarkRead(ConversationId);

        Assert.IsType<NoContentResult>(result);
        Assert.True(conversation.IsRead);
        Assert.Equal(0, conversation.UnreadCount);
        store.Verify(value => value.UpdateAsync(conversation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkRead_OfAReadConversation_SavesNothing()
    {
        var conversation = CreateForeignConversation();
        conversation.IsRead = true;
        var store = new Mock<IMessagingConversationStore>();
        var controller = CreateController(conversation, allowConversation: true, store: store);

        await controller.MarkRead(ConversationId);

        store.Verify(value => value.UpdateAsync(It.IsAny<MessagingConversation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarkRead_WhenTheThreadIsNotTheCallers_ReturnsForbid_AndLeavesItUnread()
    {
        var conversation = CreateForeignConversation();
        conversation.IsRead = false;
        var store = new Mock<IMessagingConversationStore>();
        var controller = CreateController(conversation, allowConversation: false, store: store);

        var result = await controller.MarkRead(ConversationId);

        Assert.IsType<ForbidResult>(result);
        Assert.False(conversation.IsRead);
        store.Verify(value => value.UpdateAsync(It.IsAny<MessagingConversation>(), It.IsAny<CancellationToken>()), Times.Never);
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

        var result = await controller.Send(ConversationId, "hello", subject: null, attachments: null);

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

    // Where the sender lands is decided by the conversation as the transfer left it, not as it was: once it waits in a
    // queue they do not serve they can no longer open it, while in a queue they serve they still can.
    [Fact]
    public async Task Transfer_ToAQueueTheSenderDoesNotServe_ReturnsThemToTheirList()
    {
        var result = await TransferToQueueNineAsync(senderQueueIds: ["q-1"]);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Index), redirect.ActionName);
    }

    [Fact]
    public async Task Transfer_ToAQueueTheSenderServes_KeepsThemOnTheConversation()
    {
        var result = await TransferToQueueNineAsync(senderQueueIds: ["q-9"]);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Conversation), redirect.ActionName);
    }

    // Live, a supervisor could not take over a conversation another agent held: their own name was never offered.
    [Fact]
    public async Task TransferAgents_ReturnsThePeopleItCanGoTo_IncludingTheSignedInUser()
    {
        var conversation = CreateForeignConversation();
        var controller = CreateController(
            conversation,
            allowConversation: true,
            targetUser: Mock.Of<IUser>(),
            agents:
            [
                new AgentProfile { ItemId = "agent-1", UserId = "user-1", DisplayName = "Sam Supervisor" },
                new AgentProfile { ItemId = "agent-2", UserId = "user-2", DisplayName = "Bea Recipient" },
            ]);

        var result = await controller.TransferAgents(ConversationId, query: null);

        var json = Assert.IsType<JsonResult>(result);
        var targets = Assert.IsAssignableFrom<IEnumerable<MessagingTransferTarget>>(json.Value);
        Assert.Equal(["agent-2", "agent-1"], targets.Select(target => target.Value));
    }

    [Fact]
    public async Task TransferQueues_ReturnsTheQueuesItCanBeSentBackTo()
    {
        var queues = new Mock<IActivityQueueManager>();
        queues
            .Setup(manager => manager.GetEnabledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ActivityQueue { ItemId = "q-1", Name = "Billing", Enabled = true },
                new ActivityQueue { ItemId = "q-2", Name = "Accounts", Enabled = true },
            ]);

        var controller = CreateController(CreateForeignConversation(), allowConversation: true, queueManager: queues.Object);

        var result = await controller.TransferQueues(ConversationId, query: null);

        var json = Assert.IsType<JsonResult>(result);
        var targets = Assert.IsAssignableFrom<IEnumerable<MessagingTransferTarget>>(json.Value);
        Assert.Equal(["Accounts", "Billing"], targets.Select(target => target.Text));
    }

    // Opening a conversation reads it for the menu count, and picks up a routed one so it is not reassigned away from
    // the agent reading it.
    [Fact]
    public async Task Conversation_WhenOpened_MarksItReadAndPicksItUp_SavingOnce()
    {
        var conversation = CreateForeignConversation();
        conversation.IsRead = false;
        conversation.UnreadCount = 3;
        conversation.AssignedUtc = DateTime.UtcNow;
        conversation.ReassignmentAttempts = 2;
        var store = new Mock<IMessagingConversationStore>();
        var controller = CreateController(conversation, allowConversation: true, store: store);

        var result = await controller.Conversation(ConversationId, show: null, channel: null);

        Assert.IsType<ViewResult>(result);
        Assert.True(conversation.IsRead);
        Assert.Equal(0, conversation.UnreadCount);
        Assert.Null(conversation.AssignedUtc);
        Assert.Equal(0, conversation.ReassignmentAttempts);
        store.Verify(value => value.UpdateAsync(conversation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Conversation_WhenAlreadyReadAndPickedUp_SavesNothing()
    {
        var conversation = CreateForeignConversation();
        conversation.IsRead = true;
        conversation.UnreadCount = 0;
        conversation.AssignedUtc = null;
        var store = new Mock<IMessagingConversationStore>();
        var controller = CreateController(conversation, allowConversation: true, store: store);

        var result = await controller.Conversation(ConversationId, show: null, channel: null);

        Assert.IsType<ViewResult>(result);
        store.Verify(value => value.UpdateAsync(It.IsAny<MessagingConversation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Attention_WithoutTheWorkspacePermission_ReturnsForbid()
    {
        var controller = CreateController(CreateForeignConversation(), allowConversation: true, grantWorkspace: false);

        var result = await controller.Attention();

        Assert.IsType<ForbidResult>(result);
    }

    // The badge on Messaging > Inbox reads this on every admin page.
    [Fact]
    public async Task Attention_ReturnsTheNumberOfConversationsWaitingOnTheUser()
    {
        var store = new Mock<IMessagingConversationStore>();
        store
            .Setup(value => value.CountAsync(It.Is<MessagingInboxQuery>(query => query.Filter == MessagingInboxFilter.Mine), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);
        store
            .Setup(value => value.CountAsync(It.Is<MessagingInboxQuery>(query => query.Filter == MessagingInboxFilter.Unassigned), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var controller = CreateController(CreateForeignConversation(), allowConversation: true, store: store);

        var result = Assert.IsType<JsonResult>(await controller.Attention());

        Assert.Equal(5, (int)result.Value.GetType().GetProperty("count").GetValue(result.Value));
    }

    // The sender, agent-1, holds the conversation and sends it to q-9; they may open it while they hold it, or while it
    // waits in a queue they serve.
    private static Task<IActionResult> TransferToQueueNineAsync(string[] senderQueueIds)
    {
        var conversation = CreateForeignConversation();
        conversation.OwnerId = "agent-1";
        conversation.AssignedAgentId = "agent-1";

        var transferService = new Mock<IMessagingConversationTransferService>();
        transferService
            .Setup(service => service.TransferAsync(It.IsAny<MessagingTransferRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MessagingTransferResult
            {
                Succeeded = true,
                Conversation = new MessagingConversation
                {
                    Channel = "SMS",
                    ItemId = ConversationId,
                    OwnerType = ConversationOwnerType.Queue,
                    OwnerId = "q-9",
                    AssignmentStatus = ConversationAssignmentStatus.Pooled,
                },
                Event = new MessagingConversationEvent { ToQueueId = "q-9", ToName = "Escalations" },
            });

        var controller = CreateController(
            conversation,
            allowConversation: true,
            transferService: transferService,
            conversationRule: (candidate, operation) =>
                operation != ConversationOperation.View ||
                candidate.AssignedAgentId == "agent-1" ||
                senderQueueIds.Contains(candidate.OwnerId));

        return controller.Transfer(ConversationId, ConversationRouteTargetType.Queue, targetAgentId: null, targetQueueId: "q-9", note: null);
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
        IUser targetUser = null,
        Mock<IMessagingConversationStore> store = null,
        Func<MessagingConversation, ConversationOperation, bool> conversationRule = null,
        IEnumerable<AgentProfile> agents = null,
        IActivityQueueManager queueManager = null,
        bool grantWorkspace = true)
    {
        var conversationStore = store ?? new Mock<IMessagingConversationStore>();

        conversationStore
            .Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        // Opening a conversation renders the inbox beside it and reads its thread; both come back empty here.
        conversationStore
            .Setup(store => store.QueryAsync(It.IsAny<MessagingInboxQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var session = new Mock<YesSqlSession> { DefaultValue = DefaultValue.Mock };

        var availabilityService = new Mock<IMessagingAvailabilityService>();
        availabilityService
            .Setup(service => service.Get(It.IsAny<AgentProfile>()))
            .Returns(new MessagingAgentAvailability());

        var agentProfileManager = new Mock<IAgentProfileManager>();

        agentProfileManager
            .Setup(manager => manager.FindByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "user-1" });

        var clock = new Mock<IClock>();
        clock.SetupGet(instance => instance.UtcNow).Returns(DateTime.UtcNow);

        var channels = MessagingTestChannels.Resolver(MessagingTestChannels.AcceptingDispatcher().Object, session.Object);
        var authorizationService = new ConversationAuthorizationService(allowConversation, deniedOperations, conversationRule, grantWorkspace);
        IActivityQueueManager[] queueManagers = queueManager is null ? [] : [queueManager];

        agentProfileManager
            .Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((agents ?? []).ToArray());

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
            queueManagers,
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
            new PermissiveAgentEntitlementPolicy(),
            availabilityService.Object,
            Mock.Of<IMessagingAgentNameProvider>(),
            new MessagingFavoritesService(Mock.Of<IAgentProfileManager>(), Mock.Of<IClock>()),
            queueManagers,
            Mock.Of<IContentManager>(),
            authorizationService,
            new MessagingQuietHoursGuard(
                Mock.Of<IBusinessHoursGate>(),
                Mock.Of<IMessagingQueuePolicyReader>(),
                Mock.Of<IMessagingContactTimeZoneResolver>(),
                clock.Object),
            Mock.Of<IDisplayManager<MessagingConversation>>(),
            Mock.Of<IUpdateModelAccessor>(),
            session.Object,
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
            new MessagingAttachmentUploads(
                Mock.Of<IMessagingAttachmentStore>(),
                NullLogger<MessagingAttachmentUploads>.Instance,
                new NullStringLocalizer<MessagingAttachmentUploads>()),
            Mock.Of<CrestApps.OrchardCore.ContactCenter.Core.Services.IAgentAddressResolver>(resolver =>
                resolver.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()) == Task.FromResult(new CrestApps.OrchardCore.ContactCenter.Core.Models.AgentAddresses())),
            authorizationService,
            Mock.Of<INotifier>(),
            NullLogger<AdminController>.Instance,
            new NullHtmlLocalizer(),
            new NullStringLocalizer<AdminController>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Test")),
            },
        };

        // Opening a conversation builds the links of its channel tabs.
        controller.Url = Mock.Of<IUrlHelper>();

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
    // MessagingConversationAuthorizationHandler does at runtime. A rule, when given, reads the conversation it is asked
    // about, so a test can grant what depends on the state a transfer left the conversation in.
    private sealed class ConversationAuthorizationService : IAuthorizationService
    {
        private readonly bool _allowConversation;
        private readonly ISet<ConversationOperation> _deniedOperations;
        private readonly Func<MessagingConversation, ConversationOperation, bool> _rule;
        private readonly bool _grantWorkspace;

        public ConversationAuthorizationService(
            bool allowConversation,
            ISet<ConversationOperation> deniedOperations = null,
            Func<MessagingConversation, ConversationOperation, bool> rule = null,
            bool grantWorkspace = true)
        {
            _allowConversation = allowConversation;
            _deniedOperations = deniedOperations ?? new HashSet<ConversationOperation>();
            _rule = rule;
            _grantWorkspace = grantWorkspace;
        }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            IEnumerable<IAuthorizationRequirement> requirements)
        {
            if (resource is ConversationAuthorizationResource conversationResource &&
                (!_allowConversation ||
                    _deniedOperations.Contains(conversationResource.Operation) ||
                    (_rule is not null && !_rule(conversationResource.Conversation, conversationResource.Operation))))
            {
                return Task.FromResult(AuthorizationResult.Failed());
            }

            var permissions = requirements
                .OfType<PermissionRequirement>()
                .Select(requirement => requirement.Permission.Name)
                .ToArray();

            var denied = permissions.Contains(MessagingPermissions.ViewAllConversations.Name) ||
                (!_grantWorkspace && permissions.Contains(MessagingPermissions.UseMessagingWorkspace.Name));

            return Task.FromResult(denied
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
