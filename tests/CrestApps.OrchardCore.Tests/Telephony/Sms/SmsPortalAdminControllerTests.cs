using System.Security.Claims;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Controllers;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models;
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

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

public sealed class SmsPortalAdminControllerTests
{
    private const string ConversationId = "conv-1";

    [Fact]
    public async Task Conversation_WhenTheThreadIsNotTheCallers_ReturnsForbid()
    {
        var conversation = CreateForeignConversation();
        var controller = CreateController(conversation, allowConversation: false);

        var result = await controller.Conversation(ConversationId);

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
        var conversationService = new Mock<ISmsConversationService>();
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
        var conversationService = new Mock<ISmsConversationService>();
        var controller = CreateController(conversation, allowConversation: false, conversationService: conversationService);

        var result = await controller.Send(ConversationId, "hello");

        Assert.IsType<ForbidResult>(result);
        conversationService.Verify(
            service => service.SendAsync(It.IsAny<SmsSendRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SetStatus_WhenTheThreadIsNotTheCallers_ReturnsForbid_AndNeverUpdates()
    {
        var conversation = CreateForeignConversation();
        var conversationService = new Mock<ISmsConversationService>();
        var controller = CreateController(conversation, allowConversation: false, conversationService: conversationService);

        var result = await controller.SetStatus(ConversationId, SmsConversationStatus.Closed);

        Assert.IsType<ForbidResult>(result);
        conversationService.Verify(
            service => service.SetStatusAsync(It.IsAny<string>(), It.IsAny<SmsConversationStatus>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SetStatus_WhenTheThreadIsTheCallers_UpdatesAndRedirects()
    {
        var conversation = CreateForeignConversation();
        var conversationService = new Mock<ISmsConversationService>();

        conversationService
            .Setup(service => service.SetStatusAsync(It.IsAny<string>(), It.IsAny<SmsConversationStatus>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SmsSendResult { Succeeded = true });

        var controller = CreateController(conversation, allowConversation: true, conversationService: conversationService);

        var result = await controller.SetStatus(ConversationId, SmsConversationStatus.Closed);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminController.Conversation), redirect.ActionName);
        conversationService.Verify(
            service => service.SetStatusAsync(ConversationId, SmsConversationStatus.Closed, It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Claim_WhenTheThreadDoesNotExist_ReturnsNotFound()
    {
        var controller = CreateController(conversation: null, allowConversation: true);

        var result = await controller.Claim(ConversationId);

        Assert.IsType<NotFoundResult>(result);
    }

    private static SmsConversation CreateForeignConversation()
        => new()
        {
            ItemId = ConversationId,
            ServiceAddress = "+15553334444",
            ContactAddress = "+15551112222",
            OwnerType = SmsConversationOwnerType.Personal,
            OwnerId = "agent-other",
            AssignedAgentId = "agent-other",
            AssignmentStatus = SmsConversationAssignmentStatus.Assigned,
        };

    private static AdminController CreateController(
        SmsConversation conversation,
        bool allowConversation,
        Mock<ISmsConversationService> conversationService = null)
    {
        var conversationStore = new Mock<ISmsConversationStore>();

        conversationStore
            .Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(conversation);

        var agentProfileManager = new Mock<IAgentProfileManager>();

        agentProfileManager
            .Setup(manager => manager.FindByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "user-1" });

        var clock = new Mock<IClock>();
        clock.SetupGet(instance => instance.UtcNow).Returns(DateTime.UtcNow);

        var controller = new AdminController(
            conversationStore.Object,
            (conversationService ?? new Mock<ISmsConversationService>()).Object,
            Mock.Of<ISmsBroadcastManager>(),
            Mock.Of<ISmsTemplateManager>(),
            Mock.Of<IOmnichannelChannelEndpointManager>(),
            agentProfileManager.Object,
            MockUserManager().Object,
            Mock.Of<IDisplayNameProvider>(),
            Mock.Of<ISmsAgentAvailabilityService>(),
            Mock.Of<IContentManager>(),
            new ConversationAuthorizationService(allowConversation),
            new SmsQuietHoursGuard(
                Mock.Of<IBusinessHoursGate>(),
                Mock.Of<ISmsQueuePolicyReader>(),
                Mock.Of<ISmsContactTimeZoneResolver>(),
                clock.Object),
            Mock.Of<IDisplayManager<SmsConversation>>(),
            Mock.Of<IUpdateModelAccessor>(),
            Mock.Of<INotifier>(),
            Mock.Of<YesSqlSession>(),
            clock.Object,
            Mock.Of<IOmnichannelContactTypeProvider>(),
            new OptionsWrapper<SmsPortalOptions>(new SmsPortalOptions()),
            new NullHtmlLocalizer(),
            new NullStringLocalizer());

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
    // SmsConversationAuthorizationHandler does at runtime.
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
            if (resource is SmsConversationAuthorizationResource && !_allowConversation)
            {
                return Task.FromResult(AuthorizationResult.Failed());
            }

            var isSupervisorCheck = requirements
                .OfType<PermissionRequirement>()
                .Any(requirement => requirement.Permission.Name == SmsPortalPermissions.ViewAllConversations.Name);

            return Task.FromResult(isSupervisorCheck
                ? AuthorizationResult.Failed()
                : AuthorizationResult.Success());
        }

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Success());
    }

    private sealed class NullStringLocalizer : IStringLocalizer<AdminController>
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
