using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContentFields.Fields;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Modules.Omnichannel.AITools;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.ContentFields.Fields;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.Flows.Models;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using YesSqlSession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging.AITools;

/// <summary>
/// The services the messaging workspace tools need: the real workspace builder and quiet-hours guard over doubles of
/// the conversation store, the conversation service and the agent directory, and in-memory template and broadcast
/// managers.
/// </summary>
internal sealed class MessagingToolHarness
{
    public Dictionary<string, MessagingConversation> Conversations { get; } = new(StringComparer.Ordinal);

    public FakeTemplateManager Templates { get; } = new();

    public FakeBroadcastManager Broadcasts { get; } = new();

    public FakeCatalogManager<OmnichannelChannelEndpoint> Endpoints { get; } = new();

    public Mock<IMessagingConversationService> ConversationService { get; } = new();

    public Mock<IMessagingAvailabilityService> Availability { get; } = new();

    public Mock<IMessagingConversationStore> Store { get; } = new();

    public Mock<IBusinessHoursGate> BusinessHours { get; } = new();

    public Mock<IMessagingQueuePolicyReader> QueuePolicies { get; } = new();

    public AgentProfile Agent { get; set; } = new() { ItemId = "agent-1", UserId = AgentToolTestContext.UserId };

    public List<MessagingInboxQuery> Queries { get; } = [];

    public Dictionary<string, ContentItem> Contacts { get; } = new(StringComparer.Ordinal);

    public MessagingToolHarness()
    {
        Store.Setup(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(id is not null && Conversations.TryGetValue(id, out var conversation) ? conversation : null));
        Store.Setup(store => store.FindByContactAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string channel, string address, CancellationToken _) => Conversations.Values.FirstOrDefault(conversation =>
                string.Equals(conversation.Channel, channel, StringComparison.OrdinalIgnoreCase) && conversation.ContactAddress == address));
        Store.Setup(store => store.QueryAsync(It.IsAny<MessagingInboxQuery>(), It.IsAny<CancellationToken>()))
            .Callback((MessagingInboxQuery query, CancellationToken _) => Queries.Add(query))
            .ReturnsAsync(() => Conversations.Values.ToList());
        Store.Setup(store => store.CountAsync(It.IsAny<MessagingInboxQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Conversations.Count);
        Store.Setup(store => store.GetForCustomerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        Availability.Setup(service => service.Get(It.IsAny<AgentProfile>())).Returns(new MessagingAgentAvailability());
    }

    public IServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);
        var provider = services.BuildServiceProvider();
        var authorizationService = provider.GetRequiredService<IAuthorizationService>();
        var session = new Mock<YesSqlSession> { DefaultValue = DefaultValue.Mock };
        var channels = MessagingTestChannels.Resolver(MessagingTestChannels.AcceptingDispatcher().Object, session.Object);

        var clock = new Mock<IClock>();
        clock.SetupGet(instance => instance.UtcNow).Returns(new DateTime(2026, 11, 18, 15, 0, 0, DateTimeKind.Utc));

        var agents = new Mock<IAgentProfileManager>();
        agents.Setup(manager => manager.FindByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Agent);

        var endpointManager = new Mock<IOmnichannelChannelEndpointManager>();
        endpointManager.Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken token) => Endpoints.FindByIdAsync(id, token));
        endpointManager.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>()))
            .Returns((CancellationToken token) => Endpoints.GetAllAsync(token));

        var quietHours = new MessagingQuietHoursGuard(
            BusinessHours.Object,
            QueuePolicies.Object,
            Mock.Of<IMessagingContactTimeZoneResolver>(),
            clock.Object,
            new NullStringLocalizer<MessagingQuietHoursGuard>());

        var workspaceBuilder = new MessagingWorkspaceBuilder(
            Store.Object,
            channels,
            endpointManager.Object,
            Templates,
            new LiquidMessageTemplateRenderer(Doubles.LiquidTestClocks.In("UTC"), Doubles.RecipeRoundTrip.Localizer<LiquidMessageTemplateRenderer>(), NullLogger<LiquidMessageTemplateRenderer>.Instance),
            agents.Object,
            new PermissiveAgentEntitlementPolicy(),
            Availability.Object,
            Mock.Of<IMessagingAgentNameProvider>(),
            new MessagingFavoritesService(Mock.Of<IAgentProfileManager>(), clock.Object),
            [],
            [],
            Mock.Of<IContentManager>(),
            authorizationService,
            quietHours,
            Mock.Of<IDisplayManager<MessagingConversation>>(),
            Mock.Of<IUpdateModelAccessor>(),
            session.Object,
            clock.Object,
            new MessagingAttentionCountCache(clock.Object),
            new OptionsWrapper<MessagingWorkspaceOptions>(new MessagingWorkspaceOptions()),
            new NullStringLocalizer<MessagingWorkspaceBuilder>());

        services.AddSingleton(workspaceBuilder);
        services.AddSingleton(quietHours);
        services.AddSingleton(channels);
        services.AddSingleton(Store.Object);
        services.AddSingleton(endpointManager.Object);
        services.AddSingleton<IMessageTemplateManager>(Templates);
        services.AddSingleton<IMessagingBroadcastManager>(Broadcasts);
        services.AddSingleton(ConversationService.Object);
        services.AddSingleton(Availability.Object);
        services.AddSingleton(agents.Object);
        services.AddSingleton<IAgentEntitlementPolicy>(new PermissiveAgentEntitlementPolicy());
        services.AddSingleton(new MessagingContactSearch(channels, Mock.Of<IOmnichannelContactTypeProvider>(), Mock.Of<IContentManager>(), session.Object));

        // The CRM contacts a broadcast is sent to, by content item id; every item of the Contact type is a contact.
        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync((IEnumerable<string> ids, VersionOptions _) => ids
                .Where(Contacts.ContainsKey)
                .Select(id => Contacts[id])
                .ToList());
        services.AddSingleton(contentManager.Object);

        var contactTypes = new Mock<IOmnichannelContactTypeProvider>();
        contactTypes.Setup(provider => provider.GetContactContentTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<string>)["Contact"]);
        services.AddSingleton(contactTypes.Object);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Adds a CRM contact with one mobile number in its contact methods.
    /// </summary>
    public ContentItem AddContact(string id, string mobileNumber, string contentType = "Contact")
    {
        var contact = new ContentItem { ContentItemId = id, ContentType = contentType, DisplayText = id };

        if (mobileNumber is not null)
        {
            var phone = new ContentItem { ContentType = OmnichannelConstants.ContentTypes.PhoneNumber, ContentItemId = $"{id}-phone" };
            phone.Alter<PhoneNumberInfoPart>(part =>
            {
                part.Number = new PhoneField { PhoneNumber = mobileNumber };
                part.Type = new TextField { Text = "Cell" };
            });

            var bagPart = new BagPart();
            bagPart.ContentItems.Add(phone);
            contact.Apply(OmnichannelConstants.NamedParts.ContactMethods, bagPart);
        }

        Contacts[id] = contact;

        return contact;
    }

    public MessagingConversation AddConversation(string id = "conversation-1", Action<MessagingConversation> configure = null)
    {
        var conversation = new MessagingConversation
        {
            ItemId = id,
            Channel = "SMS",
            ServiceAddress = "+15550109000",
            ContactAddress = "+15550107788",
            Status = ConversationStatus.Open,
            OwnerType = ConversationOwnerType.Personal,
            OwnerId = "agent-1",
            AssignedAgentId = "agent-1",
            AssignmentStatus = ConversationAssignmentStatus.Assigned,
            LastMessagePreview = "Where is my order?",
            AISessionId = "session-secret",
        };

        configure?.Invoke(conversation);
        Conversations[id] = conversation;

        return conversation;
    }

    /// <summary>
    /// Allows everything except the conversation operations given.
    /// </summary>
    public static Func<Permission, object, bool> Deny(params ConversationOperation[] operations)
        => (_, resource) => resource is not ConversationAuthorizationResource conversation || !operations.Contains(conversation.Operation);

    private sealed class NullStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}

/// <summary>
/// An in-memory message template manager.
/// </summary>
internal sealed class FakeTemplateManager : FakeCatalogManager<MessageTemplate>, IMessageTemplateManager
{
    /// <summary>
    /// Lists the templates of a scope by name, as the template store does; an empty scope lists the messaging
    /// workspace's own.
    /// </summary>
    public Task<IReadOnlyList<MessageTemplate>> ListByScopeAsync(string scope, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<MessageTemplate>>([.. Items
            .Where(template => string.IsNullOrEmpty(scope) ? string.IsNullOrEmpty(template.Scope) : template.Scope == scope)
            .OrderBy(template => template.Name, StringComparer.Ordinal)]);
}

/// <summary>
/// An in-memory broadcast manager.
/// </summary>
internal sealed class FakeBroadcastManager : FakeCatalogManager<MessagingBroadcast>, IMessagingBroadcastManager
{
}
