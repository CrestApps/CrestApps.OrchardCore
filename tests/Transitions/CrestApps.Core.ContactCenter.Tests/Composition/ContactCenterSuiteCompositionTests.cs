using CrestApps.Core.Builders;
using CrestApps.Core.Data.YesSql.Telephony;
using CrestApps.Core.Data.YesSql.ContactCenter;
using CrestApps.Core.Data.YesSql.ContactCenter.Services;
using CrestApps.Core.ContactCenter.Services;
using CrestApps.Core.Hosting;
using CrestApps.Core.Hosting.Locking;
using CrestApps.Core.Hosting.Background;
using CrestApps.Core.Locking;
using CrestApps.Core.Omnichannel;
using CrestApps.Core.Omnichannel.Services;
using CrestApps.Core.PhoneNumbers;
using CrestApps.Core.Telephony;
using CrestApps.Core.Telephony.Services;
using CrestApps.Core.WebSockets;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using YesSql.Indexes;

namespace CrestApps.Core.ContactCenter.Tests.Composition;

/// <summary>
/// Composes the suite the way a host does, and pins what each call registers.
/// </summary>
/// <remarks>
/// <para>
/// The builder is the package's front door: it is what a host writes, and it is the only part of the suite
/// that no Orchard Core startup exercises, because the host registers through the underlying
/// <c>AddCore*</c> methods instead. Without these tests the entry point of the package would be the one part
/// of it nothing runs.
/// </para>
/// <para>
/// The assertions are on the service collection rather than on a built provider, because what is being
/// pinned is which services a call contributes and with what lifetime - not whether an object graph that
/// needs a database can be constructed.
/// </para>
/// </remarks>
public sealed class ContactCenterSuiteCompositionTests
{
    [Fact]
    public void AddContactCenterSuite_RegistersTheHostSeamsAndNothingElse()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCrestAppsCore(crestApps => crestApps.AddContactCenterSuite());

        // Assert
        AssertRegistered<IDistributedLockProvider, LocalDistributedLockProvider>(services, ServiceLifetime.Singleton);
        AssertRegistered<ITenantAccessor, SingleTenantAccessor>(services, ServiceLifetime.Singleton);
        AssertRegistered<IScopedWorkExecutor, ServiceProviderScopedWorkExecutor>(services, ServiceLifetime.Singleton);
        AssertRegistered<IDetachedWorkExecutor, ServiceProviderDetachedWorkExecutor>(services, ServiceLifetime.Singleton);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(TimeProvider));

        // Asking for the suite is not asking for a pillar.
        AssertNotRegistered<ITelephonyService>(services);
        AssertNotRegistered<IPhoneNumberService>(services);
        AssertNotRegistered<IWebSocketConnectionRegistry>(services);
        AssertNotRegistered<IOmnichannelChannelEndpointManager>(services);
    }

    /// <summary>
    /// Pins that a queue nothing drains is not registered.
    /// </summary>
    /// <remarks>
    /// What commits a unit of work is the store package a host chose, so the drain belongs with the store. A
    /// no-op queue would accept work and drop it silently; a missing one fails to resolve at startup, which
    /// is the failure a host can act on.
    /// </remarks>
    [Fact]
    public void AddContactCenterSuite_DoesNotRegisterAnAfterCommitQueueNothingDrains()
    {
        var services = new ServiceCollection();

        services.AddCrestAppsCore(crestApps => crestApps.AddContactCenterSuite());

        AssertNotRegistered<IAfterCommitTaskQueue>(services);
    }

    [Fact]
    public void AddTelephony_RegistersOnlyTheFeaturesTheHostNamed()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCrestAppsCore(crestApps => crestApps
            .AddContactCenterSuite(suite => suite
                .AddTelephony(telephony => telephony.AddCalling())));

        // Assert
        AssertRegistered<ITelephonyService, DefaultTelephonyService>(services, ServiceLifetime.Scoped);
        AssertRegistered<IIncomingCallDispatcher, DefaultIncomingCallDispatcher>(services, ServiceLifetime.Scoped);

        // The features that were not named.
        AssertNotRegistered<IVoiceIngressGate>(services);
        AssertNotRegistered<ITelephonyUserTokenStore>(services);
        AssertNotRegistered<ITelephonyExtensionManager>(services);
        AssertNotRegistered<ITelephonyInteractionStore>(services);
    }

    /// <summary>
    /// Pins that call history can be recorded without a soft phone.
    /// </summary>
    /// <remarks>
    /// Three services take the notifier as a required dependency, so without a default a host that wanted
    /// call history and no soft phone could not resolve any of them.
    /// </remarks>
    [Fact]
    public void AddTelephony_RegistersASilentSoftPhoneNotifierByDefault()
    {
        var services = new ServiceCollection();

        services.AddCrestAppsCore(crestApps => crestApps
            .AddContactCenterSuite(suite => suite.AddTelephony()));

        AssertRegistered<ITelephonySoftPhoneNotifier, NullTelephonySoftPhoneNotifier>(services, ServiceLifetime.Scoped);
    }

    /// <summary>
    /// Pins that naming a hub leaves exactly one notifier behind.
    /// </summary>
    /// <remarks>
    /// Two descriptors for one contract resolve by "last one wins", which is a registration order nobody
    /// stated and nothing tests. The hub-bound notifier replaces the default rather than shadowing it.
    /// </remarks>
    [Fact]
    public void AddSoftPhoneNotifier_ReplacesTheSilentDefaultRatherThanShadowingIt()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCrestAppsCore(crestApps => crestApps
            .AddContactCenterSuite(suite => suite
                .AddTelephony(telephony => telephony.AddSoftPhoneNotifier<SampleTelephonyHub>())));

        // Assert
        var descriptors = services.Where(descriptor => descriptor.ServiceType == typeof(ITelephonySoftPhoneNotifier)).ToList();

        Assert.Single(descriptors);
        Assert.Equal(typeof(TelephonySoftPhoneNotifier<SampleTelephonyHub>), descriptors[0].ImplementationType);
    }

    /// <summary>
    /// Pins that persistence is a separate choice from the services that read through it.
    /// </summary>
    [Fact]
    public void AddYesSqlStores_IsWhatBringsTheTelephonyStoresIn()
    {
        // Arrange
        var withoutStores = new ServiceCollection();
        var withStores = new ServiceCollection();

        // Act
        withoutStores.AddCrestAppsCore(crestApps => crestApps
            .AddContactCenterSuite(suite => suite
                .AddTelephony(telephony => telephony.AddExtensions().AddInteractions())));

        withStores.AddCrestAppsCore(crestApps => crestApps
            .AddContactCenterSuite(suite => suite
                .AddTelephony(telephony => telephony.AddExtensions().AddInteractions().AddYesSqlStores())));

        // Assert
        AssertRegistered<ITelephonyExtensionManager, TelephonyExtensionManager>(withoutStores, ServiceLifetime.Scoped);
        AssertNotRegistered<ITelephonyExtensionStore>(withoutStores);

        AssertRegistered<ITelephonyExtensionStore>(withStores);
        AssertRegistered<ITelephonyInteractionStore>(withStores);
        Assert.Equal(2, withStores.Count(descriptor => descriptor.ServiceType == typeof(IIndexProvider)));
    }

    /// <summary>
    /// Pins that the reconciliation cycle is available without a runner, and that asking for the worker adds one.
    /// </summary>
    /// <remarks>
    /// A host with its own scheduler owns when a cycle runs. A host with none asks for the worker instead,
    /// and gets the same cycle plus something to drive it.
    /// </remarks>
    [Fact]
    public void AddInteractions_LeavesTheCycleForTheHostToSchedule()
    {
        // Arrange
        var scheduled = new ServiceCollection();
        var unscheduled = new ServiceCollection();

        // Act
        unscheduled.AddCrestAppsCore(crestApps => crestApps
            .AddContactCenterSuite(suite => suite.AddTelephony(telephony => telephony.AddInteractions())));

        scheduled.AddCrestAppsCore(crestApps => crestApps
            .AddContactCenterSuite(suite => suite.AddTelephony(telephony => telephony.AddInteractionsWorker())));

        // Assert
        AssertRegistered<ITelephonyInteractionReconciliationCycle>(unscheduled);
        Assert.DoesNotContain(unscheduled, descriptor => descriptor.ImplementationType == typeof(CycleRunner<ITelephonyInteractionReconciliationCycle>));

        AssertRegistered<ITelephonyInteractionReconciliationCycle>(scheduled);
        Assert.Contains(scheduled, descriptor => descriptor.ImplementationType == typeof(CycleRunner<ITelephonyInteractionReconciliationCycle>));
    }

    [Fact]
    public void AddOmnichannel_RegistersOnlyTheFeaturesTheHostNamed()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCrestAppsCore(crestApps => crestApps
            .AddContactCenterSuite(suite => suite
                .AddOmnichannel(omnichannel => omnichannel.AddChannelEndpoints())));

        // Assert
        AssertRegistered<IOmnichannelChannelEndpointManager, OmnichannelChannelEndpointManager>(services, ServiceLifetime.Scoped);
        AssertNotRegistered<IAutomatedConversationGate>(services);
    }

    [Fact]
    public void AddOmnichannelAutomation_RegistersTheGateAndTheHandoffTurn()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCrestAppsCore(crestApps => crestApps
            .AddContactCenterSuite(suite => suite
                .AddOmnichannel(omnichannel => omnichannel.AddAutomation())));

        // Assert
        AssertRegistered<IAutomatedConversationGate, InMemoryAutomatedConversationGate>(services, ServiceLifetime.Singleton);
        AssertRegistered<IOmnichannelHandoffTurn, OmnichannelHandoffTurn>(services, ServiceLifetime.Scoped);
    }

    [Fact]
    public void AddPhoneNumbersAndWebSockets_AreSeparateChoices()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCrestAppsCore(crestApps => crestApps
            .AddContactCenterSuite(suite => suite
                .AddPhoneNumbers()
                .AddWebSockets()));

        // Assert
        AssertRegistered<IPhoneNumberService, DefaultPhoneNumberService>(services, ServiceLifetime.Singleton);
        AssertRegistered<IWebSocketConnectionRegistry, InMemoryWebSocketConnectionRegistry>(services, ServiceLifetime.Singleton);
    }

    /// <summary>
    /// Pins that the defaults yield to a host that registered its own first.
    /// </summary>
    /// <remarks>
    /// Every framework default uses <c>TryAdd</c> for this reason. A host that runs more than one node
    /// replaces the per-node socket registry, and it must keep the one it chose.
    /// </remarks>
    [Fact]
    public void TheFrameworkDefaults_YieldToAHostThatRegisteredItsOwn()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<IWebSocketConnectionRegistry, SampleWebSocketConnectionRegistry>();
        services.AddSingleton<ITenantAccessor, SampleTenantAccessor>();

        // Act
        services.AddCrestAppsCore(crestApps => crestApps
            .AddContactCenterSuite(suite => suite.AddWebSockets()));

        // Assert
        AssertRegistered<IWebSocketConnectionRegistry, SampleWebSocketConnectionRegistry>(services, ServiceLifetime.Singleton);
        AssertRegistered<ITenantAccessor, SampleTenantAccessor>(services, ServiceLifetime.Singleton);
    }

    /// <summary>
    /// Pins that composing the same pillar twice is not composing it twice.
    /// </summary>
    /// <remarks>
    /// Two hosts' worth of registrations for one contract resolve by order, and an index provider registered
    /// twice indexes every document twice.
    /// </remarks>
    [Fact]
    public void ComposingTheSuiteTwice_RegistersEachServiceOnce()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        for (var i = 0; i < 2; i++)
        {
            services.AddCrestAppsCore(crestApps => crestApps
                .AddContactCenterSuite(suite => suite
                    .AddPhoneNumbers()
                    .AddWebSockets()
                    .AddTelephony(telephony => telephony.AddExtensions().AddYesSqlStores())));
        }

        // Assert
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IPhoneNumberService));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IWebSocketConnectionRegistry));
        Assert.Equal(2, services.Count(descriptor => descriptor.ServiceType == typeof(IIndexProvider)));
    }

    [Fact]
    public void AddContactCenter_RegistersOnlyTheFeaturesTheHostNamed()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.AddCrestAppsCore(crestApps => crestApps
            .AddContactCenterSuite(suite => suite
                .AddContactCenter(contactCenter => contactCenter.AddAgentDirectory())));

        // Assert
        AssertRegistered<IAgentProfileManager, AgentProfileManager>(services, ServiceLifetime.Scoped);
        AssertRegistered<IAgentEntitlementPolicy, PermissiveAgentEntitlementPolicy>(services, ServiceLifetime.Scoped);
        AssertRegistered<IAgentQueueMembershipReader, AgentQueueMembershipReader>(services, ServiceLifetime.Scoped);

        // The features that were not named.
        AssertNotRegistered<IRecordingAccessGovernanceService>(services);
        AssertNotRegistered<IContactCenterVoiceMediaProviderResolver>(services);
        AssertNotRegistered<IDialerStrategy>(services);
    }

    /// <summary>
    /// Pins that asking for the contact centre is not asking for a feature of it.
    /// </summary>
    [Fact]
    public void AddContactCenter_WithoutNamingAFeature_RegistersNothing()
    {
        // Arrange
        var services = new ServiceCollection();
        var baseline = new ServiceCollection();

        baseline.AddCrestAppsCore(crestApps => crestApps.AddContactCenterSuite());

        // Act
        services.AddCrestAppsCore(crestApps => crestApps
            .AddContactCenterSuite(suite => suite.AddContactCenter()));

        // Assert
        Assert.Equal(baseline.Count, services.Count);
    }

    /// <summary>
    /// Pins that the contact centre's persistence is a separate choice from the services that read through it,
    /// the same way telephony's is.
    /// </summary>
    [Fact]
    public void AddAgentDirectoryYesSqlStores_IsWhatBringsTheAgentStoreIn()
    {
        // Arrange
        var withoutStores = new ServiceCollection();
        var withStores = new ServiceCollection();

        // Act
        withoutStores.AddCrestAppsCore(crestApps => crestApps
            .AddContactCenterSuite(suite => suite
                .AddContactCenter(contactCenter => contactCenter.AddAgentDirectory())));

        withStores.AddCrestAppsCore(crestApps => crestApps
            .AddContactCenterSuite(suite => suite
                .AddContactCenter(contactCenter => contactCenter
                    .AddAgentDirectory()
                    .AddAgentDirectoryYesSqlStores())));

        // Assert
        AssertNotRegistered<IAgentProfileStore>(withoutStores);
        AssertRegistered<IAgentProfileStore, AgentProfileStore>(withStores, ServiceLifetime.Scoped);
    }

    private static void AssertRegistered<TService, TImplementation>(IServiceCollection services, ServiceLifetime lifetime)
    {
        var descriptor = Assert.Single(services, candidate => candidate.ServiceType == typeof(TService));

        Assert.Equal(typeof(TImplementation), descriptor.ImplementationType);
        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    private static void AssertRegistered<TService>(IServiceCollection services, ServiceLifetime? lifetime = null)
    {
        var descriptor = Assert.Single(services, candidate => candidate.ServiceType == typeof(TService));

        if (lifetime is not null)
        {
            Assert.Equal(lifetime.Value, descriptor.Lifetime);
        }
    }

    private static void AssertNotRegistered<TService>(IServiceCollection services)
        => Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(TService));

    private sealed class SampleTelephonyHub : Hub<ITelephonyClient>
    {
    }

    private sealed class SampleWebSocketConnectionRegistry : IWebSocketConnectionRegistry
    {
        public Task<WebSocketRendezvous> RegisterAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult<WebSocketRendezvous>(null);

        public Task<WebSocketRendezvous> TryClaimAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult<WebSocketRendezvous>(null);

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class SampleTenantAccessor : ITenantAccessor
    {
        public string TenantName => "Sample";
    }
}
