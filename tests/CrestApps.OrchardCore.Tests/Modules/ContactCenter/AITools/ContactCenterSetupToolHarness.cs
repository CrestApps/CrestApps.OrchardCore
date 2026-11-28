using System.Globalization;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Core.Handlers;
using CrestApps.OrchardCore.BusinessHours.Core.Services;
using CrestApps.OrchardCore.BusinessHours.Models;
using CrestApps.OrchardCore.BusinessHours.Services;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.PhoneNumbers;
using CrestApps.OrchardCore.PhoneNumbers.Core.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Descriptor.Models;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.AITools;

/// <summary>
/// Builds the services of a Contact Center setup tool call: the real catalog managers and catalog handlers of queues,
/// queue groups, entry points, business-hours calendars, reason codes and dialer profiles over in-memory stores, so the
/// tools are held to the same validation the admin pages are; agent profiles and presence are doubles.
/// </summary>
internal sealed class ContactCenterSetupToolHarness
{
    public static readonly DateTime Now = new(2026, 11, 18, 15, 0, 0, DateTimeKind.Utc);

    public QueueStore Queues { get; } = new();

    public QueueGroupStore QueueGroups { get; } = new();

    public EntryPointStore EntryPoints { get; } = new();

    public InMemoryNamedCatalog<BusinessHoursSchedule> Calendars { get; } = new();

    public ReasonCodeStore ReasonCodes { get; } = new();

    public DialerProfileStore DialerProfiles { get; } = new();

    public AddressStore Addresses { get; } = new();

    public List<AgentProfile> Agents { get; } = [];

    public List<AgentProfile> CreatedAgents { get; } = [];

    public Mock<IAgentPresenceManager> Presence { get; } = new();

    public Dictionary<string, string> Users { get; } = new(StringComparer.OrdinalIgnoreCase) { ["maria"] = "user-maria", ["omar"] = "user-omar" };

    public List<ContactCenterExternalDestination> ExternalDestinations { get; } = [];

    public HashSet<string> Features { get; } = new(StringComparer.Ordinal)
    {
        ContactCenterConstants.Feature.Queues,
        ContactCenterConstants.Feature.EntryPoints,
        ContactCenterConstants.Feature.InboundVoice,
        ContactCenterConstants.Feature.AgentEntitlements,
        ContactCenterConstants.Feature.DialerPaced,
    };

    /// <summary>
    /// Gets or sets a last change to the services, made before the provider is built.
    /// </summary>
    public Action<IServiceCollection> Configure { get; set; }

    public IServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);

        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughStringLocalizer<>));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton(CreateClock());
        services.AddSingleton(Mock.Of<IContactCenterConfigurationCache>());

        services.AddSingleton<IActivityQueueStore>(Queues);
        services.AddScoped<ICatalogEntryHandler<ActivityQueue>, ActivityQueueHandler>();
        services.AddScoped<IActivityQueueManager, ActivityQueueManager>();

        services.AddSingleton<IActivityQueueGroupStore>(QueueGroups);
        services.AddScoped<ICatalogEntryHandler<ActivityQueueGroup>, ActivityQueueGroupHandler>();
        services.AddScoped<IActivityQueueGroupManager, ActivityQueueGroupManager>();

        services.AddSingleton<IContactCenterEntryPointStore>(EntryPoints);
        services.AddSingleton<IOmnichannelChannelEndpointStore>(Addresses);
        services.AddScoped<ICatalogEntryHandler<ContactCenterEntryPoint>, ContactCenterEntryPointHandler>();
        services.AddScoped<IContactCenterEntryPointManager, ContactCenterEntryPointManager>();
        services.AddSingleton(Options.Create(new EntryPointAIAgentOptions()));

        var channels = new EntryPointChannelOptions();
        channels.Channels["Phone"] = new EntryPointChannel { Name = "Phone" };
        channels.Channels["SMS"] = new EntryPointChannel { Name = "SMS" };
        services.AddSingleton(Options.Create(channels));

        services.AddSingleton<INamedCatalog<BusinessHoursSchedule>>(Calendars);
        services.AddSingleton<ICatalog<BusinessHoursSchedule>>(Calendars);
        services.AddScoped<ICatalogEntryHandler<BusinessHoursSchedule>, BusinessHoursScheduleHandler>();
        services.AddScoped<INamedCatalogManager<BusinessHoursSchedule>, NamedCatalogManager<BusinessHoursSchedule>>();
        services.AddScoped<IHoursScheduleSource, BusinessHoursScheduleSource>();
        services.AddScoped<IOpenHoursService, DefaultOpenHoursService>();
        services.AddScoped<IBusinessHoursService, DefaultBusinessHoursService>();

        services.AddSingleton<IAgentStateReasonCodeStore>(ReasonCodes);
        services.AddScoped<ICatalogEntryHandler<AgentStateReasonCode>, AgentStateReasonCodeHandler>();
        services.AddScoped<IAgentStateReasonCodeManager, AgentStateReasonCodeManager>();

        services.AddSingleton<IDialerProfileStore>(DialerProfiles);
        services.AddScoped<ICatalogEntryHandler<DialerProfile>, DialerProfileHandler>();
        services.AddScoped<IDialerProfileManager, DialerProfileManager>();
        services.AddSingleton<IPhoneNumberService>(new DefaultPhoneNumberService());
        services.AddSingleton(CreateFeaturesManager());
        services.AddSingleton(Mock.Of<IDialerPacingStatisticsProvider>());

        services.AddSingleton(CreateAgentManager());
        services.AddSingleton(Presence.Object);
        services.AddSingleton(CreateUserManager());
        services.AddSingleton(AdminFormOptionsProviderFactory.Create(Queues.Entries));

        services.AddSingleton(CreateSiteService());

        var descriptor = new ShellDescriptor();

        foreach (var feature in Features)
        {
            descriptor.Features.Add(new ShellFeature(feature));
        }

        services.AddSingleton(descriptor);

        Configure?.Invoke(services);

        return services.BuildServiceProvider();
    }

    public ActivityQueue AddQueue(string id, string name, Action<ActivityQueue> configure = null)
    {
        var queue = new ActivityQueue { ItemId = id, Name = name, CreatedUtc = Now.AddDays(-10) };
        configure?.Invoke(queue);
        Queues.Entries.Add(queue);

        return queue;
    }

    // A business-hours calendar is a Business Hours schedule: open 09:00 to 17:00 on weekdays, in UTC.
    public BusinessHoursSchedule AddCalendar(string id, string name, Action<BusinessHoursSchedule> configure = null)
    {
        var calendar = new BusinessHoursSchedule
        {
            ItemId = id,
            Name = name,
            Hours = new HoursSchedule
            {
                TimeZoneId = "UTC",
                Windows = [.. Enum.GetValues<DayOfWeek>()
                    .Where(day => day is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                    .Select(day => new HoursWindow { Day = day, OpenMinute = 540, CloseMinute = 1020 })],
            },
        };

        configure?.Invoke(calendar);
        Calendars.Entries.Add(calendar);

        return calendar;
    }

    public AgentProfile AddAgent(string id, string userName, Action<AgentProfile> configure = null)
    {
        var agent = new AgentProfile
        {
            ItemId = id,
            UserId = Users.GetValueOrDefault(userName) ?? $"user-{userName}",
            UserName = userName,
            Name = Users.GetValueOrDefault(userName) ?? $"user-{userName}",
            DisplayName = userName,
        };

        configure?.Invoke(agent);
        Agents.Add(agent);

        return agent;
    }

    private static IClock CreateClock()
    {
        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(Now);
        clock.Setup(value => value.GetTimeZones()).Returns(new[] { "UTC", "America/New_York", "Europe/Paris" }.Select(id => Mock.Of<ITimeZone>(zone => zone.TimeZoneId == id)).ToArray());

        return clock.Object;
    }

    private IShellFeaturesManager CreateFeaturesManager()
    {
        var manager = new Mock<IShellFeaturesManager>();
        manager.Setup(value => value.GetEnabledFeaturesAsync())
            .ReturnsAsync(() => Features.Select(id => Mock.Of<IFeatureInfo>(feature => feature.Id == id)).ToList());

        return manager.Object;
    }

    private IAgentProfileManager CreateAgentManager()
    {
        var manager = new Mock<IAgentProfileManager>();
        manager.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => Agents.ToList());
        manager.Setup(value => value.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(Agents.FirstOrDefault(agent => agent.ItemId == id)));
        manager.Setup(value => value.FindByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, CancellationToken _) => Agents.FirstOrDefault(agent => agent.UserId == userId));
        manager.Setup(value => value.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new AgentProfile { ItemId = "new-agent" }));
        manager.Setup(value => value.ValidateAsync(It.IsAny<AgentProfile>(), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(new ValidationResultDetails()));
        manager.Setup(value => value.CreateAsync(It.IsAny<AgentProfile>(), It.IsAny<CancellationToken>()))
            .Returns((AgentProfile agent, CancellationToken _) =>
            {
                CreatedAgents.Add(agent);
                Agents.Add(agent);

                return ValueTask.CompletedTask;
            });

        return manager.Object;
    }

    private UserManager<IUser> CreateUserManager()
    {
        var manager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        manager.Setup(value => value.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((string name) => Users.TryGetValue(name, out var userId) ? new User { UserName = name, UserId = userId } : null);
        manager.Setup(value => value.GetUserIdAsync(It.IsAny<IUser>())).ReturnsAsync((IUser user) => ((User)user).UserId);
        manager.Setup(value => value.GetUserNameAsync(It.IsAny<IUser>())).ReturnsAsync((IUser user) => user.UserName);

        return manager.Object;
    }

    private ISiteService CreateSiteService()
    {
        var site = new Mock<ISite>();
        site.Setup(value => value.GetOrCreate<ContactCenterExternalTransferSettings>())
            .Returns(() => new ContactCenterExternalTransferSettings { Destinations = ExternalDestinations });

        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return siteService.Object;
    }

    internal class TestCatalogStore<T> : InMemoryCatalog<T>
        where T : CatalogItem
    {
        public Task<T> FindByNameAsync(string name, CancellationToken cancellationToken = default)
            => Task.FromResult(Entries.FirstOrDefault(entry => string.Equals((entry as INameAwareModel)?.Name, name, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyCollection<T>> GetEnabledAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<T>>(Entries.ToList());
    }

    internal sealed class QueueStore : TestCatalogStore<ActivityQueue>, IActivityQueueStore;

    internal sealed class QueueGroupStore : TestCatalogStore<ActivityQueueGroup>, IActivityQueueGroupStore;

    internal sealed class EntryPointStore : TestCatalogStore<ContactCenterEntryPoint>, IContactCenterEntryPointStore;

    internal sealed class ReasonCodeStore : TestCatalogStore<AgentStateReasonCode>, IAgentStateReasonCodeStore;

    internal sealed class DialerProfileStore : TestCatalogStore<DialerProfile>, IDialerProfileStore;

    internal sealed class AddressStore : InMemoryCatalog<OmnichannelChannelEndpoint>, IOmnichannelChannelEndpointStore
    {
        public ValueTask<OmnichannelChannelEndpoint> GetByServiceAddressAsync(string channel, string serviceAddress, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Entries.FirstOrDefault(entry => entry.Value == serviceAddress));
    }

    private sealed class PassThroughStringLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name]
            => new(name, name);

        public LocalizedString this[string name, params object[] arguments]
            => new(name, string.Format(CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => [];
    }
}
