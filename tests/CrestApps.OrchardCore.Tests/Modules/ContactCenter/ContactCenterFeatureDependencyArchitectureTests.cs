using System.Text;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Architecture tests that parse the Contact Center manifests and startup ownership to verify that each
/// feature owns exactly the services, background tasks, and integration glue it is meant to, so a service
/// moving to the wrong feature is caught as a named failure rather than passing review as an ordinary edit.
/// </summary>
public sealed class ContactCenterFeatureDependencyArchitectureTests
{
    private const string ContactCenterManifestPath = "src/Modules/CrestApps.OrchardCore.ContactCenter/Manifest.cs";
    private const string ContactCenterModulePath = "src/Modules/CrestApps.OrchardCore.ContactCenter";
    private const string AsteriskManifestPath = "src/Modules/CrestApps.OrchardCore.Asterisk/Manifest.cs";
    private const string AsteriskModulePath = "src/Modules/CrestApps.OrchardCore.Asterisk";
    private const string DialpadManifestPath = "src/Modules/CrestApps.OrchardCore.Dialpad/Manifest.cs";
    private const string DialpadModulePath = "src/Modules/CrestApps.OrchardCore.Dialpad";
    private const string SignalRManifestPath = "src/Modules/CrestApps.OrchardCore.SignalR/Manifest.cs";
    private const string SignalRStartupPath = "src/Modules/CrestApps.OrchardCore.SignalR/Startup.cs";
    private const string TelephonyManifestPath = "src/Modules/CrestApps.OrchardCore.Telephony/Manifest.cs";
    private const string TelephonyStartupPath = "src/Modules/CrestApps.OrchardCore.Telephony/Startup.cs";
    private const string OmnichannelManagementsManifestPath = "src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Manifest.cs";
    private const string OmnichannelManagementsStartupPath = "src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Startup.cs";

    private static readonly string[] ContactCenterConcreteTypeSearchDirectories =
    [
        "src/Modules/CrestApps.OrchardCore.ContactCenter",
        "src/Core/CrestApps.OrchardCore.ContactCenter.Core",
        "src/Abstractions/CrestApps.OrchardCore.ContactCenter.Abstractions",
    ];

    [Fact]
    public void BaseFeature_ComposesOmnichannelManagementForItsAdministrationSurface()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var features = ParseManifestFeatures(repositoryRoot, ContactCenterManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);

        // Act
        var baseDependencies = features["CrestApps.OrchardCore.ContactCenter"].Dependencies
            .Order(StringComparer.Ordinal);

        // Assert
        Assert.Equal(
            ["CrestApps.OrchardCore.Omnichannel.Managements"],
            baseDependencies);
    }

    [Fact]
    public void VoiceFeature_IsServerOnly_AndSoftPhoneProjectionIsIntegrationGlue()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var features = ParseManifestFeatures(repositoryRoot, ContactCenterManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var startupClasses = ParseStartupClassesInDirectory(
            repositoryRoot,
            ContactCenterModulePath,
            ContactCenterConstantsFeatureArea(repositoryRoot));

        // Act
        var voiceDependencies = features["CrestApps.OrchardCore.ContactCenter.Voice"].Dependencies
            .Order(StringComparer.Ordinal);
        var softPhoneEventHandlerOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IContactCenterEventHandler, ContactCenterSoftPhoneEventHandler>()",
                StringComparison.Ordinal));
        var softPhoneRequiredFeatures = softPhoneEventHandlerOwner.RequiredFeatureIds
            .Order(StringComparer.Ordinal);

        // Assert
        Assert.True(features["CrestApps.OrchardCore.ContactCenter.Voice"].EnabledByDependencyOnly);
        Assert.Equal(
            [
                "CrestApps.OrchardCore.ContactCenter.Queues",
                "CrestApps.OrchardCore.ContactCenter.RealTime",
                "CrestApps.OrchardCore.ContactCenter.Recording.Core",
                "CrestApps.OrchardCore.Telephony",
            ],
            voiceDependencies);
        Assert.False(
            features.ContainsKey("CrestApps.OrchardCore.ContactCenter.Voice.SoftPhone"),
            "The soft-phone projection is integration glue and must not be declared as a selectable feature.");
        // The soft-phone projection is integration glue owned by the Voice feature and gated on Real-Time and the
        // Telephony soft phone, so it activates whenever all three capabilities are enabled without a separate toggle.
        Assert.Equal(
            "CrestApps.OrchardCore.ContactCenter.Voice",
            softPhoneEventHandlerOwner.FeatureId);
        Assert.Equal(
            [
                "CrestApps.OrchardCore.ContactCenter.RealTime",
                "CrestApps.OrchardCore.Telephony.SoftPhone",
            ],
            softPhoneRequiredFeatures);
    }

    [Fact]
    public void VoiceFeature_OwnsProviderCommandRecoveryStateMachine()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var startupClasses = ParseStartupClassesInDirectory(
            repositoryRoot,
            ContactCenterModulePath,
            ContactCenterConstantsFeatureArea(repositoryRoot));

        // Act
        var commandStoreOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IProviderCommandStore, ProviderCommandStore>()",
                StringComparison.Ordinal));
        var commandManagerOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IProviderCommandManager, ProviderCommandManager>()",
                StringComparison.Ordinal));
        var commandStateOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IProviderCommandStateService, ProviderCommandStateService>()",
                StringComparison.Ordinal));
        var commandProcessorOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IProviderCommandProcessor, ProviderCommandProcessor>()",
                StringComparison.Ordinal));
        var commandIndexOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddIndexProvider<ProviderCommandIndexProvider>()",
                StringComparison.Ordinal));
        var commandMigrationOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddDataMigration<ProviderCommandIndexMigrations>()",
                StringComparison.Ordinal));
        var commandRecoveryTaskOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "Singleton<IBackgroundTask, ProviderCommandRecoveryBackgroundTask>()",
                StringComparison.Ordinal));

        // Assert
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Voice", commandStoreOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Voice", commandManagerOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Voice", commandStateOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Voice", commandProcessorOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Voice", commandIndexOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Voice", commandMigrationOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Voice", commandRecoveryTaskOwner.FeatureId);
    }

    [Fact]
    public void QueuesFeature_DoesNotOwnSoftPhoneIntegration()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var startupClasses = ParseStartupClassesInDirectory(
            repositoryRoot,
            ContactCenterModulePath,
            ContactCenterConstantsFeatureArea(repositoryRoot));

        // Act
        var widgetOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddDisplayDriver<SoftPhoneWidget, ContactCenterSoftPhoneWidgetDisplayDriver>()",
                StringComparison.Ordinal));
        var resourceOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddResourceConfiguration<ContactCenterSoftPhoneResourceConfiguration>()",
                StringComparison.Ordinal));
        var endpointOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddAgentSoftPhoneEndpoints(adminOptions.AdminUrlPrefix)",
                StringComparison.Ordinal));
        var voice = "CrestApps.OrchardCore.ContactCenter.Voice";

        // Assert: the soft-phone projection is integration glue owned by the Voice feature and gated on the
        // Real-Time and Telephony soft-phone features rather than owned by Work Distribution.
        Assert.NotEqual("CrestApps.OrchardCore.ContactCenter.Queues", widgetOwner.FeatureId);
        Assert.Equal(voice, widgetOwner.FeatureId);
        Assert.Equal(voice, resourceOwner.FeatureId);
        Assert.Equal(voice, endpointOwner.FeatureId);
        Assert.Equal(
            [
                "CrestApps.OrchardCore.ContactCenter.RealTime",
                "CrestApps.OrchardCore.Telephony.SoftPhone",
            ],
            widgetOwner.RequiredFeatureIds.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void WorkflowBridge_ActivatesWithOrchardWorkflows_WithoutASeparateFeature()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var features = ParseManifestFeatures(repositoryRoot, ContactCenterManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var startupClasses = ParseStartupClassesInDirectory(
            repositoryRoot,
            ContactCenterModulePath,
            ContactCenterConstantsFeatureArea(repositoryRoot));

        // Act
        var workflowHandlerOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IContactCenterEventHandler, ContactCenterWorkflowEventHandler>()",
                StringComparison.Ordinal));

        // Assert. The workflow bridge is no longer an independently selectable feature: it is owned by the base
        // Contact Center feature and activates whenever Orchard Core Workflows is also enabled, so an operator does
        // not have to enable a separate feature to get workflow automation.
        Assert.DoesNotContain("CrestApps.OrchardCore.ContactCenter.Workflows", features.Keys);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter", workflowHandlerOwner.FeatureId);
        Assert.Equal(
            ["OrchardCore.Workflows"],
            workflowHandlerOwner.RequiredFeatureIds.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void WorkforceFeature_OwnsPresenceAndDurableAgentSessions()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var features = ParseManifestFeatures(repositoryRoot, ContactCenterManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var startupClasses = ParseStartupClassesInDirectory(
            repositoryRoot,
            ContactCenterModulePath,
            ContactCenterConstantsFeatureArea(repositoryRoot));

        // Act
        var queueDependencies = features["CrestApps.OrchardCore.ContactCenter.Queues"].Dependencies
            .Order(StringComparer.Ordinal);
        var realTimeDependencies = features["CrestApps.OrchardCore.ContactCenter.RealTime"].Dependencies
            .Order(StringComparer.Ordinal);
        var presenceOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IAgentPresenceManager, AgentPresenceManagerService>()",
                StringComparison.Ordinal));
        var sessionOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IAgentSessionService, AgentSessionService>()",
                StringComparison.Ordinal));
        var cleanupOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "Singleton<IBackgroundTask, AgentSessionCleanupBackgroundTask>()",
                StringComparison.Ordinal));

        // Assert
        Assert.Contains("CrestApps.OrchardCore.ContactCenter.Agents", queueDependencies);
        Assert.Contains("CrestApps.OrchardCore.ContactCenter.Queues", realTimeDependencies);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Agents", presenceOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Agents", sessionOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Agents", cleanupOwner.FeatureId);
    }

    [Fact]
    public void WorkDistributionFeature_OwnsStrategiesAndAssignment()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var features = ParseManifestFeatures(repositoryRoot, ContactCenterManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var startupClasses = ParseStartupClassesInDirectory(
            repositoryRoot,
            ContactCenterModulePath,
            ContactCenterConstantsFeatureArea(repositoryRoot));

        // Act
        var voiceDependencies = features["CrestApps.OrchardCore.ContactCenter.Voice"].Dependencies
            .Order(StringComparer.Ordinal);
        var routingServiceOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IActivityRoutingService, ActivityRoutingService>()",
                StringComparison.Ordinal));
        var assignmentOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IActivityAssignmentService, ActivityAssignmentService>()",
                StringComparison.Ordinal));
        var assignmentTaskOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "Singleton<IBackgroundTask, ReservationExpiryBackgroundTask>()",
                StringComparison.Ordinal));

        // Assert
        Assert.Contains("CrestApps.OrchardCore.ContactCenter.Queues", voiceDependencies);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Queues", routingServiceOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Queues", assignmentOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Queues", assignmentTaskOwner.FeatureId);
    }

    [Fact]
    public void AgentDesktopWorkspace_IsIntegrationGlueGatedOnItsCapabilities()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var features = ParseManifestFeatures(repositoryRoot, ContactCenterManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var startupClasses = ParseStartupClassesInDirectory(
            repositoryRoot,
            ContactCenterModulePath,
            ContactCenterConstantsFeatureArea(repositoryRoot));
        var area = ContactCenterConstantsFeatureArea(repositoryRoot);

        // Act
        var endpointOwner = startupClasses.Single(startup =>
            startup.Body.Contains("AddAgentWorkspaceEndpoints()", StringComparison.Ordinal));
        var navigationOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddNavigationProvider<ContactCenterAgentDesktopAdminMenu>()",
                StringComparison.Ordinal));
        var softPhoneWorkView = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CrestApps.OrchardCore.ContactCenter",
            "Views",
            "Items",
            "ContactCenterSoftPhoneWork.View.cshtml"));

        // Assert: the agent desktop is no longer a separately selectable feature. Its workspace surface is
        // integration glue owned by the base feature and gated on the agents, real-time, voice, and Telephony
        // soft-phone capabilities it composes, so it activates automatically once a provider is wired for
        // voice and the soft phone is enabled rather than requiring an operator to enable a redundant toggle.
        Assert.False(
            features.ContainsKey("CrestApps.OrchardCore.ContactCenter.AgentDesktop"),
            "The agent desktop workspace is integration glue and must not be declared as a selectable feature.");
        Assert.Equal(area, endpointOwner.FeatureId);
        Assert.Equal(area, navigationOwner.FeatureId);
        Assert.Equal(
            [
                "CrestApps.OrchardCore.ContactCenter.Agents",
                "CrestApps.OrchardCore.ContactCenter.RealTime",
                "CrestApps.OrchardCore.ContactCenter.Voice",
                "CrestApps.OrchardCore.Telephony.SoftPhone",
            ],
            endpointOwner.RequiredFeatureIds.Order(StringComparer.Ordinal));
        Assert.Equal(
            [
                "CrestApps.OrchardCore.ContactCenter.Agents",
                "CrestApps.OrchardCore.ContactCenter.RealTime",
                "CrestApps.OrchardCore.ContactCenter.Voice",
                "CrestApps.OrchardCore.Telephony.SoftPhone",
            ],
            navigationOwner.RequiredFeatureIds.Order(StringComparer.Ordinal));
        Assert.Contains(
            "Url.Action(\"Index\", \"AgentWorkspace\", new { area = ContactCenterConstants.Feature.Area }) ?? returnUrl",
            softPhoneWorkView,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SupervisionFeature_OwnsDashboardSurface()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var features = ParseManifestFeatures(repositoryRoot, ContactCenterManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var startupClasses = ParseStartupClassesInDirectory(
            repositoryRoot,
            ContactCenterModulePath,
            ContactCenterConstantsFeatureArea(repositoryRoot));

        // Act
        var dependencies = features["CrestApps.OrchardCore.ContactCenter.Supervision"].Dependencies
            .Order(StringComparer.Ordinal);
        var endpointOwner = startupClasses.Single(startup =>
            startup.Body.Contains("AddSupervisorDashboardEndpoints()", StringComparison.Ordinal));
        var navigationOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddNavigationProvider<ContactCenterSupervisionAdminMenu>()",
                StringComparison.Ordinal));

        // Assert
        Assert.Equal(
            [
                "CrestApps.OrchardCore.ContactCenter.RealTime",
                "CrestApps.OrchardCore.ContactCenter.Voice",
            ],
            dependencies);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Supervision", endpointOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Supervision", navigationOwner.FeatureId);
    }

    [Fact]
    public void DialerFeature_OwnsMandatoryOutboundEligibilityAndAttempts()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var startupClasses = ParseStartupClassesInDirectory(
            repositoryRoot,
            ContactCenterModulePath,
            ContactCenterConstantsFeatureArea(repositoryRoot));

        // Act
        var eligibilityOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IDialerEligibilityService, DefaultDialerEligibilityService>()",
                StringComparison.Ordinal));
        var attemptOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IDialerAttemptService, DialerAttemptService>()",
                StringComparison.Ordinal));

        // Assert
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Dialer", eligibilityOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Dialer", attemptOwner.FeatureId);
    }

    [Fact]
    public void PacedDialingFeature_OwnsStrategiesAndPacing()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var features = ParseManifestFeatures(repositoryRoot, ContactCenterManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var startupClasses = ParseStartupClassesInDirectory(
            repositoryRoot,
            ContactCenterModulePath,
            ContactCenterConstantsFeatureArea(repositoryRoot));

        // Act
        var dependencies = features["CrestApps.OrchardCore.ContactCenter.Dialer.Paced"].Dependencies
            .Order(StringComparer.Ordinal);
        var strategyOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IDialerStrategy, PowerDialerStrategy>()",
                StringComparison.Ordinal));
        var pacingOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "Singleton<IBackgroundTask, DialerPacingBackgroundTask>()",
                StringComparison.Ordinal));

        // Assert
        Assert.Equal(
            ["CrestApps.OrchardCore.ContactCenter.Dialer"],
            dependencies);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Dialer.Paced", strategyOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Dialer.Paced", pacingOwner.FeatureId);
    }

    [Fact]
    public void InboundVoiceFeature_OwnsInboundQualificationSurface()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var features = ParseManifestFeatures(repositoryRoot, ContactCenterManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var startupClasses = ParseStartupClassesInDirectory(
            repositoryRoot,
            ContactCenterModulePath,
            ContactCenterConstantsFeatureArea(repositoryRoot));

        // Act
        var dependencies = features["CrestApps.OrchardCore.ContactCenter.InboundVoice"].Dependencies
            .Order(StringComparer.Ordinal);
        var resolverOwner = startupClasses.Single(startup =>
            startup.Body.Contains("AddScoped<IEntryPointResolver, EntryPointResolver>()", StringComparison.Ordinal));
        var ingressOwner = startupClasses.Single(startup =>
            startup.Body.Contains("AddVoiceIngressEndpoint()", StringComparison.Ordinal));
        var navigationOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddNavigationProvider<ContactCenterEntryPointsAdminMenu>()",
                StringComparison.Ordinal));
        var inboundServiceOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IInboundVoiceService>(sp => sp.GetRequiredService<VoiceContactCenterCallRouter>())",
                StringComparison.Ordinal));

        // Assert
        Assert.Equal(
            [
                "CrestApps.OrchardCore.ContactCenter.Queues",
                "CrestApps.OrchardCore.ContactCenter.Voice",
            ],
            dependencies);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.InboundVoice", resolverOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.InboundVoice", ingressOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.InboundVoice", navigationOwner.FeatureId);
        Assert.Empty(navigationOwner.RequiredFeatureIds);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Voice", inboundServiceOwner.FeatureId);
    }

    [Fact]
    public void RecordingFeature_OwnsRecordingOrchestration()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var features = ParseManifestFeatures(repositoryRoot, ContactCenterManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var startupClasses = ParseStartupClassesInDirectory(
            repositoryRoot,
            ContactCenterModulePath,
            ContactCenterConstantsFeatureArea(repositoryRoot));

        // Act
        var dependencies = features["CrestApps.OrchardCore.ContactCenter.Recording"].Dependencies
            .Order(StringComparer.Ordinal);
        var recordingOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IContactCenterRecordingService, ContactCenterRecordingService>()",
                StringComparison.Ordinal));

        // Assert
        Assert.Equal(
            [
                "CrestApps.OrchardCore.ContactCenter.Recording.Core",
                "CrestApps.OrchardCore.ContactCenter.Voice",
            ],
            dependencies);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Recording", recordingOwner.FeatureId);
    }

    [Fact]
    public void VoiceMediaAndProviderAdapters_HaveExplicitFeatureOwnership()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var contactCenterFeatures = ParseManifestFeatures(repositoryRoot, ContactCenterManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var contactCenterStartups = ParseStartupClassesInDirectory(
            repositoryRoot,
            ContactCenterModulePath,
            "CrestApps.OrchardCore.ContactCenter");
        var asteriskFeatures = ParseManifestFeatures(repositoryRoot, AsteriskManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var asteriskStartups = ParseStartupClassesInDirectory(
            repositoryRoot,
            AsteriskModulePath,
            "CrestApps.OrchardCore.Asterisk");
        var dialPadFeatures = ParseManifestFeatures(repositoryRoot, DialpadManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var dialPadStartups = ParseStartupClassesInDirectory(
            repositoryRoot,
            DialpadModulePath,
            "CrestApps.OrchardCore.Dialpad");

        // Act
        var mediaDependencies = contactCenterFeatures["CrestApps.OrchardCore.ContactCenter.Voice.Media"].Dependencies;
        var mediaResolverOwner = contactCenterStartups.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IContactCenterVoiceMediaProviderResolver, ContactCenterVoiceMediaProviderResolver>()",
                StringComparison.Ordinal));
        var asteriskBaseDependencies = asteriskFeatures["CrestApps.OrchardCore.Asterisk"].Dependencies;
        var asteriskVoiceOwner = asteriskStartups.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IContactCenterVoiceProvider, AsteriskContactCenterVoiceProvider>()",
                StringComparison.Ordinal));
        var contactCenterVoiceProjectionOwner = contactCenterStartups.Single(startup =>
            startup.Body.Contains(
                "AddScoped<INormalizedVoiceEventHandler, ContactCenterVoiceProjection>()",
                StringComparison.Ordinal));
        var asteriskContactCenterReconcilerOwner = asteriskStartups.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IAsteriskProviderStateReconciler, AsteriskContactCenterProviderStateReconciler>()",
                StringComparison.Ordinal));
        var asteriskMediaOwner = asteriskStartups.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IContactCenterVoiceMediaProvider, AsteriskContactCenterVoiceMediaProvider>()",
                StringComparison.Ordinal));
        var dialPadBaseDependencies = dialPadFeatures["CrestApps.OrchardCore.Dialpad"].Dependencies;
        var dialPadVoiceOwner = dialPadStartups.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IContactCenterVoiceProvider>(sp => sp.GetRequiredService<DialpadContactCenterVoiceProvider>())",
                StringComparison.Ordinal));
        var asteriskBaseStartup = asteriskStartups.Single(startup =>
            startup.FeatureId == "CrestApps.OrchardCore.Asterisk"
            && startup.RequiredFeatureIds.Count == 0);

        // Assert: the Contact Center Voice Media boundary stays an owned Contact Center feature, but a provider's
        // participation in it (and in Contact Center Voice) is no longer a dedicated per-provider feature. Each
        // provider adapter is integration glue owned by the provider's own module feature and gated with
        // [RequireFeatures] on the provider module plus the Contact Center capability it composes, so it
        // activates automatically once the provider and the capability are both enabled.
        Assert.Equal(["CrestApps.OrchardCore.ContactCenter.Voice"], mediaDependencies);
        Assert.True(contactCenterFeatures["CrestApps.OrchardCore.ContactCenter.Voice.Media"].EnabledByDependencyOnly);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Voice.Media", mediaResolverOwner.FeatureId);
        Assert.False(
            asteriskFeatures.ContainsKey("CrestApps.OrchardCore.Asterisk.ContactCenterVoice"),
            "The Asterisk Contact Center voice adapter is integration glue and must not be a selectable feature.");
        Assert.False(
            asteriskFeatures.ContainsKey("CrestApps.OrchardCore.Asterisk.ContactCenterMedia"),
            "The Asterisk Contact Center media adapter is integration glue and must not be a selectable feature.");
        Assert.False(
            dialPadFeatures.ContainsKey("CrestApps.OrchardCore.Dialpad.ContactCenterVoice"),
            "The Dialpad Contact Center voice adapter is integration glue and must not be a selectable feature.");
        Assert.Equal(["CrestApps.OrchardCore.Telephony"], asteriskBaseDependencies);
        Assert.Equal("CrestApps.OrchardCore.Asterisk", asteriskVoiceOwner.FeatureId);
        // The adapter is owned by the Asterisk module's default feature, so it only requires the Contact Center
        // capability it composes; requiring the Asterisk feature it already belongs to would be redundant.
        Assert.Equal(
            [
                "CrestApps.OrchardCore.ContactCenter.Voice",
            ],
            asteriskVoiceOwner.RequiredFeatureIds.Order(StringComparer.Ordinal));
        Assert.Equal("CrestApps.OrchardCore.Asterisk", asteriskMediaOwner.FeatureId);
        Assert.Equal(
            [
                "CrestApps.OrchardCore.ContactCenter.Voice.Media",
            ],
            asteriskMediaOwner.RequiredFeatureIds.Order(StringComparer.Ordinal));
        // The Contact Center projection is a peer consumer of the provider-neutral normalized stream, so it
        // is owned by the Contact Center voice feature rather than by any one provider module. A provider
        // module that owned it would be able to absorb the stream and starve every other projection.
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Voice", contactCenterVoiceProjectionOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.Asterisk", asteriskContactCenterReconcilerOwner.FeatureId);
        Assert.Equal(
            [
                "CrestApps.OrchardCore.ContactCenter.Voice",
            ],
            asteriskContactCenterReconcilerOwner.RequiredFeatureIds.Order(StringComparer.Ordinal));
        Assert.DoesNotContain("IProviderVoiceEventService", asteriskBaseStartup.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("IProviderCallStateSynchronizationService", asteriskBaseStartup.Body, StringComparison.Ordinal);
        Assert.Equal(["CrestApps.OrchardCore.Telephony"], dialPadBaseDependencies);
        Assert.Equal("CrestApps.OrchardCore.Dialpad", dialPadVoiceOwner.FeatureId);
        Assert.Equal(
            [
                "CrestApps.OrchardCore.ContactCenter.Voice",
                "CrestApps.OrchardCore.Dialpad",
            ],
            dialPadVoiceOwner.RequiredFeatureIds.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void ProviderModules_ReferenceOnlyStableContactCenterAbstractions()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var asteriskProject = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src/Modules/CrestApps.OrchardCore.Asterisk/CrestApps.OrchardCore.Asterisk.csproj"));
        var dialPadProject = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src/Modules/CrestApps.OrchardCore.Dialpad/CrestApps.OrchardCore.Dialpad.csproj"));

        // Act
        var providerProjects = new[]
        {
            asteriskProject,
            dialPadProject,
        };

        // Assert
        Assert.All(providerProjects, project =>
        {
            Assert.Contains("CrestApps.OrchardCore.ContactCenter.Abstractions.csproj", project, StringComparison.Ordinal);
            Assert.DoesNotContain("CrestApps.OrchardCore.ContactCenter.Core.csproj", project, StringComparison.Ordinal);
            Assert.DoesNotContain("CrestApps.OrchardCore.ContactCenter.csproj", project, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void OmnichannelManagements_UsesOwnedOptionalDialerContributors()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/CrestApps.OrchardCore.Omnichannel.Managements.csproj"));
        var sourceDirectory = Path.Combine(
            repositoryRoot,
            "src/Modules/CrestApps.OrchardCore.Omnichannel.Managements");
        var source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        // Act
        var referencesContactCenterImplementation = project.Contains(
            "CrestApps.OrchardCore.ContactCenter.Core.csproj",
            StringComparison.Ordinal);

        // Assert
        Assert.False(referencesContactCenterImplementation);
        Assert.DoesNotContain("using CrestApps.OrchardCore.ContactCenter", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetService<IDialerProfileManager>", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetService<IActivityQueueService>", source, StringComparison.Ordinal);
        Assert.Contains("IActivityDialerContributor", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PostCommitAndHubExecution_UseTheContactCenterScopeExecutor()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var sourceFiles = new[]
        {
            "src/Core/CrestApps.OrchardCore.ContactCenter.Core/Services/DefaultContactCenterEventPublisher.cs",
            "src/Modules/CrestApps.OrchardCore.ContactCenter/Handlers/ContactCenterRealTimeEventHandler.cs",
            "src/Modules/CrestApps.OrchardCore.ContactCenter/Handlers/OfferQueuedVoiceWorkOnAvailabilityHandler.cs",
            "src/Modules/CrestApps.OrchardCore.ContactCenter/Hubs/ContactCenterHub.cs",
            "src/Modules/CrestApps.OrchardCore.ContactCenter/Services/InboundVoiceCallProcessor.cs",
        };

        // Act
        var sources = sourceFiles.ToDictionary(
            path => path,
            path => File.ReadAllText(Path.Combine(repositoryRoot, path)));

        // Assert
        foreach (var (path, source) in sources)
        {
            Assert.DoesNotContain("ShellScope.", source, StringComparison.Ordinal);
            Assert.DoesNotContain("CreateAsyncScope(", source, StringComparison.Ordinal);
            Assert.Contains("IContactCenterScopeExecutor", source, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(
            "IServiceProvider",
            sources["src/Modules/CrestApps.OrchardCore.ContactCenter/Handlers/ContactCenterRealTimeEventHandler.cs"],
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "IServiceProvider",
            sources["src/Modules/CrestApps.OrchardCore.ContactCenter/Handlers/OfferQueuedVoiceWorkOnAvailabilityHandler.cs"],
            StringComparison.Ordinal);
    }

    private static string ContactCenterConstantsFeatureArea(string repositoryRoot)
    {
        return ResolveToken(repositoryRoot, "ContactCenterConstants.Feature.Area");
    }

    private static List<StartupClass> ParseStartupClasses(string repositoryRoot, string relativeStartupPath, string defaultFeatureId)
    {
        var path = Path.Combine(repositoryRoot, relativeStartupPath.Replace('/', Path.DirectorySeparatorChar));
        var text = File.ReadAllText(path);
        var classes = new List<StartupClass>();

        foreach (Match match in Regex.Matches(
            text,
            @"(?<attributes>(?:\[[^\]]*\]\s*)*)(?:public|internal)\s+(?:sealed\s+)?class\s+(?<name>\w+)\s*:\s*StartupBase",
            RegexOptions.Singleline))
        {
            var attributes = match.Groups["attributes"].Value;
            var featureMatch = Regex.Match(attributes, @"\[Feature\((?<id>[^)]+)\)\]");
            var featureId = featureMatch.Success
                ? ResolveToken(repositoryRoot, featureMatch.Groups["id"].Value.Trim())
                : defaultFeatureId;
            var requiredFeatureIds = new List<string>();

            foreach (Match requireFeaturesMatch in Regex.Matches(
                attributes,
                @"\[RequireFeatures\((?<ids>[^)]*)\)\]",
                RegexOptions.Singleline))
            {
                foreach (var rawFeatureId in SplitTopLevel(requireFeaturesMatch.Groups["ids"].Value, ','))
                {
                    requiredFeatureIds.Add(ResolveToken(repositoryRoot, rawFeatureId));
                }
            }

            var braceStart = text.IndexOf('{', match.Index + match.Length);
            var braceEnd = FindMatching(text, braceStart, '{', '}');
            var body = text.Substring(braceStart, braceEnd - braceStart + 1);

            classes.Add(new StartupClass(featureId, body, requiredFeatureIds));
        }

        return classes;
    }

    private static List<StartupClass> ParseStartupClassesInDirectory(
        string repositoryRoot,
        string relativeDirectory,
        string defaultFeatureId)
    {
        var directory = Path.Combine(repositoryRoot, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));
        var classes = new List<StartupClass>();

        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(repositoryRoot, file);
            classes.AddRange(ParseStartupClasses(repositoryRoot, relativePath, defaultFeatureId));
        }

        return classes;
    }

    private static List<ManifestFeature> ParseManifestFeatures(string repositoryRoot, string relativeManifestPath)
    {
        var manifestPath = Path.Combine(repositoryRoot, relativeManifestPath.Replace('/', Path.DirectorySeparatorChar));
        var text = File.ReadAllText(manifestPath);
        var features = new List<ManifestFeature>();

        const string featureToken = "[assembly: Feature(";
        var searchIndex = 0;

        while (true)
        {
            var start = text.IndexOf(featureToken, searchIndex, StringComparison.Ordinal);

            if (start < 0)
            {
                break;
            }

            var parenStart = start + featureToken.Length - 1;
            var end = FindMatching(text, parenStart, '(', ')');
            var body = text.Substring(parenStart + 1, end - parenStart - 1);

            var idMatch = Regex.Match(body, @"Id\s*=\s*(?<id>[^,]+),", RegexOptions.Singleline);
            var id = ResolveToken(repositoryRoot, idMatch.Groups["id"].Value.Trim());
            var enabledByDependencyOnly = Regex.IsMatch(
                body,
                @"EnabledByDependencyOnly\s*=\s*true\b",
                RegexOptions.Singleline);

            var dependencies = new List<string>();
            var dependenciesMatch = Regex.Match(body, @"Dependencies\s*=\s*\[(?<deps>.*?)\]", RegexOptions.Singleline);

            if (dependenciesMatch.Success)
            {
                foreach (var rawToken in dependenciesMatch.Groups["deps"].Value.Split(','))
                {
                    var trimmed = rawToken.Trim();

                    if (trimmed.Length > 0)
                    {
                        dependencies.Add(ResolveToken(repositoryRoot, trimmed));
                    }
                }
            }

            features.Add(new ManifestFeature(id, dependencies, enabledByDependencyOnly));
            searchIndex = end + 1;
        }

        if (features.Count == 0)
        {
            // A module without a separate [assembly: Feature] block uses the Module attribute's Id as its
            // single, dependency-free feature (for example, the SignalR module).
            var moduleMatch = Regex.Match(text, @"\[assembly:\s*Module\((?<body>.*?)\)\]", RegexOptions.Singleline);
            var idMatch = Regex.Match(moduleMatch.Groups["body"].Value, @"Id\s*=\s*(?<id>[^,]+),", RegexOptions.Singleline);
            var id = ResolveToken(repositoryRoot, idMatch.Groups["id"].Value.Trim());

            features.Add(new ManifestFeature(id, [], EnabledByDependencyOnly: false));
        }

        return features;
    }

    private static string ResolveToken(string repositoryRoot, string rawToken)
    {
        if (rawToken.StartsWith('"'))
        {
            return rawToken.Trim('"');
        }

        var segments = rawToken.Split('.');
        var text = ReadTypeSource(repositoryRoot, segments[0]);
        var scope = text;

        for (var i = 1; i < segments.Length - 1; i++)
        {
            scope = ExtractNestedTypeBody(scope, segments[i]);
        }

        var propertyName = segments[segments.Length - 1];
        var match = Regex.Match(scope, $@"public const string {Regex.Escape(propertyName)}\s*=\s*""(?<value>[^""]+)"";");

        if (!match.Success)
        {
            throw new InvalidOperationException($"Could not resolve manifest token '{rawToken}'.");
        }

        return match.Groups["value"].Value;
    }

    private static string ReadTypeSource(string repositoryRoot, string typeName)
    {
        var files = EnumerateSourceFiles(Path.Combine(repositoryRoot, "src"), typeName + "*.cs")
            .Where(path => IsPartialSourceOf(path, typeName))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        if (files.Length == 0)
        {
            throw new InvalidOperationException($"Expected at least one source file for type '{typeName}' under 'src', but found none.");
        }

        return string.Join(Environment.NewLine, files.Select(File.ReadAllText));
    }

    private static bool IsPartialSourceOf(string path, string typeName)
    {
        var fileName = Path.GetFileNameWithoutExtension(path);

        return string.Equals(fileName, typeName, StringComparison.Ordinal)
            || fileName.StartsWith(typeName + ".", StringComparison.Ordinal);
    }

    private static string ExtractNestedTypeBody(string text, string typeName)
    {
        var match = Regex.Match(text, $@"(?:public|internal)\s+(?:static\s+)?class\s+{Regex.Escape(typeName)}\b");

        if (!match.Success)
        {
            throw new InvalidOperationException($"Could not find nested type '{typeName}'.");
        }

        var braceStart = text.IndexOf('{', match.Index);
        var braceEnd = FindMatching(text, braceStart, '{', '}');

        return text.Substring(braceStart, braceEnd - braceStart + 1);
    }

    private static IEnumerable<string> EnumerateSourceFiles(string sourceRoot, string fileName)
    {
        var excludedDirectoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".docusaurus",
            "bin",
            "build",
            "node_modules",
            "obj",
        };
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(sourceRoot);

        while (pendingDirectories.Count > 0)
        {
            var directory = pendingDirectories.Pop();

            foreach (var file in Directory.EnumerateFiles(directory, fileName, SearchOption.TopDirectoryOnly))
            {
                yield return file;
            }

            foreach (var childDirectory in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
            {
                if (excludedDirectoryNames.Contains(Path.GetFileName(childDirectory)) ||
                    new DirectoryInfo(childDirectory).Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

                pendingDirectories.Push(childDirectory);
            }
        }
    }

    private static int FindMatching(string text, int openIndex, char openChar, char closeChar)
    {
        var depth = 0;

        for (var i = openIndex; i < text.Length; i++)
        {
            if (text[i] == openChar)
            {
                depth++;
            }
            else if (text[i] == closeChar)
            {
                depth--;

                if (depth == 0)
                {
                    return i;
                }
            }
        }

        throw new InvalidOperationException($"Unbalanced '{openChar}'/'{closeChar}' while parsing source text.");
    }

    private static List<string> SplitTopLevel(string text, char separator)
    {
        var parts = new List<string>();
        var depth = 0;
        var current = new StringBuilder();

        foreach (var ch in text)
        {
            if (ch is '<' or '(')
            {
                depth++;
            }
            else if (ch is '>' or ')')
            {
                depth--;
            }

            if (ch == separator && depth == 0)
            {
                parts.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        var last = current.ToString().Trim();

        if (last.Length > 0)
        {
            parts.Add(last);
        }

        return parts;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ??
            throw new InvalidOperationException("The repository root could not be located.");
    }

    private sealed record ManifestFeature(
        string Id,
        IReadOnlyList<string> Dependencies,
        bool EnabledByDependencyOnly);

    private sealed record StartupClass(
        string FeatureId,
        string Body,
        IReadOnlyList<string> RequiredFeatureIds);
}
