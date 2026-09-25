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
        Mock<IMessagingConversationService> conversationService = null)
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
        var authorizationService = new ConversationAuthorizationService(allowConversation);

        var workspaceBuilder = new MessagingWorkspaceBuilder(
            conversationStore.Object,
            channels,
            Mock.Of<IOmnichannelChannelEndpointManager>(),
            Mock.Of<IMessageTemplateManager>(),
            agentProfileManager.Object,
            Mock.Of<IMessagingAvailabilityService>(),
            MockUserManager().Object,
            Mock.Of<IDisplayNameProvider>(),
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

        public ConversationAuthorizationService(bool allowConversation)
        {
            _allowConversation = allowConversation;
        }

        public Task<AuthorizationResult> AuthorizeAsync(
            ClaimsPrincipal user,
            object resource,
            IEnumerable<IAuthorizationRequirement> requirements)
        {
            if (resource is ConversationAuthorizationResource && !_allowConversation)
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
