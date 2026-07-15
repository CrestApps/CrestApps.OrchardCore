using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// R0a architecture contract tests that parse the Contact Center manifests and startup ownership to detect
/// undeclared feature dependencies. These are characterization tests: they pin the currently known P0
/// violations recorded in the R0a feature-dependency ledger and fail the build the moment a new, unrecorded
/// undeclared dependency appears, without refactoring the production feature boundaries (deferred to R2).
/// </summary>
public sealed class ContactCenterFeatureDependencyArchitectureTests
{
    private const string ContactCenterManifestPath = "src/Modules/CrestApps.OrchardCore.ContactCenter/Manifest.cs";
    private const string ContactCenterModulePath = "src/Modules/CrestApps.OrchardCore.ContactCenter";
    private const string ContactCenterStartupPath = "src/Modules/CrestApps.OrchardCore.ContactCenter/Startup.cs";
    private const string AsteriskManifestPath = "src/Modules/CrestApps.OrchardCore.Asterisk/Manifest.cs";
    private const string AsteriskModulePath = "src/Modules/CrestApps.OrchardCore.Asterisk";
    private const string DialPadManifestPath = "src/Modules/CrestApps.OrchardCore.DialPad/Manifest.cs";
    private const string DialPadModulePath = "src/Modules/CrestApps.OrchardCore.DialPad";
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
    public void DeclaredExternalManifestDependencies_MatchTheExpectedLedger()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var ledger = LoadLedger(repositoryRoot);
        var features = ParseManifestFeatures(repositoryRoot, ContactCenterManifestPath);
        var contactCenterFeatureIds = features.Select(feature => feature.Id).ToHashSet(StringComparer.Ordinal);

        var expected = ledger["acceptedExternalDependencies"]!.AsArray()
            .Select(entry => (Feature: entry!["feature"]!.GetValue<string>(), DependsOn: entry["dependsOn"]!.GetValue<string>()))
            .Concat(ledger["knownViolations"]!.AsArray()
                .Where(entry => entry!["kind"]!.GetValue<string>() == "declared-manifest-coupling")
                .Select(entry => (Feature: entry!["feature"]!.GetValue<string>(), DependsOn: entry["dependsOn"]!.GetValue<string>())))
            .ToHashSet();

        // Act
        var actual = new HashSet<(string Feature, string DependsOn)>();

        foreach (var feature in features)
        {
            foreach (var dependency in feature.Dependencies)
            {
                if (!contactCenterFeatureIds.Contains(dependency))
                {
                    actual.Add((feature.Id, dependency));
                }
            }
        }

        var undeclaredInLedger = actual.Except(expected).ToList();
        var staleInLedger = expected.Except(actual).ToList();

        // Assert
        Assert.True(
            undeclaredInLedger.Count == 0,
            "Found new, unrecorded external manifest dependencies. Record each one in the R0a ledger's " +
            "'acceptedExternalDependencies' (if intentional) or 'knownViolations' (if a P0 finding): " +
            string.Join(", ", undeclaredInLedger.Select(entry => $"{entry.Feature} -> {entry.DependsOn}")));

        Assert.True(
            staleInLedger.Count == 0,
            "The R0a ledger records external manifest dependencies that no longer exist; update the ledger: " +
            string.Join(", ", staleInLedger.Select(entry => $"{entry.Feature} -> {entry.DependsOn}")));
    }

    [Fact]
    public void BaseFeature_IsHeadless_AndAdminOwnsOmnichannelManagement()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var features = ParseManifestFeatures(repositoryRoot, ContactCenterManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);

        // Act
        var baseDependencies = features["CrestApps.OrchardCore.ContactCenter"].Dependencies
            .Order(StringComparer.Ordinal);
        var adminDependencies = features["CrestApps.OrchardCore.ContactCenter.Admin"].Dependencies
            .Order(StringComparer.Ordinal);

        // Assert
        Assert.Equal(
            ["CrestApps.OrchardCore.Omnichannel"],
            baseDependencies);
        Assert.Equal(
            [
                "CrestApps.OrchardCore.ContactCenter",
                "CrestApps.OrchardCore.Omnichannel.Managements",
            ],
            adminDependencies);
    }

    [Fact]
    public void VoiceFeature_IsServerOnly_AndSoftPhoneProjectionHasExplicitFeature()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var features = ParseManifestFeatures(repositoryRoot, ContactCenterManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var startupClasses = ParseStartupClasses(
            repositoryRoot,
            ContactCenterStartupPath,
            ContactCenterConstantsFeatureArea(repositoryRoot));

        // Act
        var voiceDependencies = features["CrestApps.OrchardCore.ContactCenter.Voice"].Dependencies
            .Order(StringComparer.Ordinal);
        var softPhoneDependencies = features["CrestApps.OrchardCore.ContactCenter.Voice.SoftPhone"].Dependencies
            .Order(StringComparer.Ordinal);
        var softPhoneEventHandlerOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IContactCenterEventHandler, ContactCenterSoftPhoneEventHandler>()",
                StringComparison.Ordinal));

        // Assert
        Assert.Equal(
            [
                "CrestApps.OrchardCore.ContactCenter.Routing",
                "CrestApps.OrchardCore.Telephony",
            ],
            voiceDependencies);
        Assert.Equal(
            [
                "CrestApps.OrchardCore.ContactCenter.RealTime",
                "CrestApps.OrchardCore.ContactCenter.Voice",
                "CrestApps.OrchardCore.Telephony.SoftPhone",
            ],
            softPhoneDependencies);
        Assert.Equal(
            "CrestApps.OrchardCore.ContactCenter.Voice.SoftPhone",
            softPhoneEventHandlerOwner.FeatureId);
    }

    [Fact]
    public void VoiceFeature_OwnsProviderCommandRecoveryStateMachine()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var startupClasses = ParseStartupClasses(
            repositoryRoot,
            ContactCenterStartupPath,
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
                "AddSingleton<IBackgroundTask, ProviderCommandRecoveryBackgroundTask>()",
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
        var startupClasses = ParseStartupClasses(
            repositoryRoot,
            ContactCenterStartupPath,
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

        // Assert
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Voice.SoftPhone", widgetOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Voice.SoftPhone", resourceOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Voice.SoftPhone", endpointOwner.FeatureId);
    }

    [Fact]
    public void WorkflowBridge_HasAnIndependentlySelectableFeature()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var features = ParseManifestFeatures(repositoryRoot, ContactCenterManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var startupClasses = ParseStartupClasses(
            repositoryRoot,
            ContactCenterStartupPath,
            ContactCenterConstantsFeatureArea(repositoryRoot));

        // Act
        var dependencies = features["CrestApps.OrchardCore.ContactCenter.Workflows"].Dependencies
            .Order(StringComparer.Ordinal);
        var workflowHandlerOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IContactCenterEventHandler, ContactCenterWorkflowEventHandler>()",
                StringComparison.Ordinal));

        // Assert
        Assert.Equal(
            [
                "CrestApps.OrchardCore.ContactCenter",
                "OrchardCore.Workflows",
            ],
            dependencies);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Workflows", workflowHandlerOwner.FeatureId);
        Assert.Empty(workflowHandlerOwner.RequiredFeatureIds);
    }

    [Fact]
    public void AvailabilityFeature_OwnsPresenceAndDurableAgentSessions()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var features = ParseManifestFeatures(repositoryRoot, ContactCenterManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var startupClasses = ParseStartupClasses(
            repositoryRoot,
            ContactCenterStartupPath,
            ContactCenterConstantsFeatureArea(repositoryRoot));

        // Act
        var availabilityDependencies = features["CrestApps.OrchardCore.ContactCenter.Availability"].Dependencies
            .Order(StringComparer.Ordinal);
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
                "AddSingleton<IBackgroundTask, AgentSessionCleanupBackgroundTask>()",
                StringComparison.Ordinal));

        // Assert
        Assert.Equal(
            ["CrestApps.OrchardCore.ContactCenter.Agents"],
            availabilityDependencies);
        Assert.Contains("CrestApps.OrchardCore.ContactCenter.Availability", queueDependencies);
        Assert.Contains("CrestApps.OrchardCore.ContactCenter.Availability", realTimeDependencies);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Availability", presenceOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Availability", sessionOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Availability", cleanupOwner.FeatureId);
    }

    [Fact]
    public void RoutingFeature_OwnsStrategiesAndAssignment()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var features = ParseManifestFeatures(repositoryRoot, ContactCenterManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var startupClasses = ParseStartupClasses(
            repositoryRoot,
            ContactCenterStartupPath,
            ContactCenterConstantsFeatureArea(repositoryRoot));

        // Act
        var routingDependencies = features["CrestApps.OrchardCore.ContactCenter.Routing"].Dependencies
            .Order(StringComparer.Ordinal);
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
                "AddSingleton<IBackgroundTask, ReservationExpiryBackgroundTask>()",
                StringComparison.Ordinal));

        // Assert
        Assert.Equal(
            ["CrestApps.OrchardCore.ContactCenter.Queues"],
            routingDependencies);
        Assert.Contains("CrestApps.OrchardCore.ContactCenter.Routing", voiceDependencies);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Routing", routingServiceOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Routing", assignmentOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Routing", assignmentTaskOwner.FeatureId);
    }

    [Fact]
    public void AgentDesktopFeature_OwnsWorkspaceSurface()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var features = ParseManifestFeatures(repositoryRoot, ContactCenterManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var startupClasses = ParseStartupClasses(
            repositoryRoot,
            ContactCenterStartupPath,
            ContactCenterConstantsFeatureArea(repositoryRoot));

        // Act
        var dependencies = features["CrestApps.OrchardCore.ContactCenter.AgentDesktop"].Dependencies
            .Order(StringComparer.Ordinal);
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

        // Assert
        Assert.Equal(
            [
                "CrestApps.OrchardCore.ContactCenter.Availability",
                "CrestApps.OrchardCore.ContactCenter.RealTime",
                "CrestApps.OrchardCore.ContactCenter.Voice.SoftPhone",
                "CrestApps.OrchardCore.Omnichannel.Managements",
            ],
            dependencies);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.AgentDesktop", endpointOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.AgentDesktop", navigationOwner.FeatureId);
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
        var startupClasses = ParseStartupClasses(
            repositoryRoot,
            ContactCenterStartupPath,
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
    public void ComplianceFeature_OwnsOutboundEligibilityAndAttempts()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var features = ParseManifestFeatures(repositoryRoot, ContactCenterManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var startupClasses = ParseStartupClasses(
            repositoryRoot,
            ContactCenterStartupPath,
            ContactCenterConstantsFeatureArea(repositoryRoot));

        // Act
        var dependencies = features["CrestApps.OrchardCore.ContactCenter.Compliance"].Dependencies
            .Order(StringComparer.Ordinal);
        var eligibilityOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IDialerEligibilityService, DefaultDialerEligibilityService>()",
                StringComparison.Ordinal));
        var attemptOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IDialerAttemptService, DialerAttemptService>()",
                StringComparison.Ordinal));

        // Assert
        Assert.Equal(
            ["CrestApps.OrchardCore.ContactCenter.Dialer"],
            dependencies);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Compliance", eligibilityOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Compliance", attemptOwner.FeatureId);
    }

    [Fact]
    public void AutomatedDialerFeature_OwnsStrategiesAndPacing()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var features = ParseManifestFeatures(repositoryRoot, ContactCenterManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var startupClasses = ParseStartupClasses(
            repositoryRoot,
            ContactCenterStartupPath,
            ContactCenterConstantsFeatureArea(repositoryRoot));

        // Act
        var dependencies = features["CrestApps.OrchardCore.ContactCenter.Dialer.Automated"].Dependencies
            .Order(StringComparer.Ordinal);
        var strategyOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IDialerStrategy, PowerDialerStrategy>()",
                StringComparison.Ordinal));
        var pacingOwner = startupClasses.Single(startup =>
            startup.Body.Contains(
                "AddSingleton<IBackgroundTask, DialerPacingBackgroundTask>()",
                StringComparison.Ordinal));

        // Assert
        Assert.Equal(
            [
                "CrestApps.OrchardCore.ContactCenter.Compliance",
                "CrestApps.OrchardCore.ContactCenter.Dialer",
            ],
            dependencies);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Dialer.Automated", strategyOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Dialer.Automated", pacingOwner.FeatureId);
    }

    [Fact]
    public void EntryPointsFeature_OwnsInboundQualificationSurface()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var features = ParseManifestFeatures(repositoryRoot, ContactCenterManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var startupClasses = ParseStartupClasses(
            repositoryRoot,
            ContactCenterStartupPath,
            ContactCenterConstantsFeatureArea(repositoryRoot));

        // Act
        var dependencies = features["CrestApps.OrchardCore.ContactCenter.EntryPoints"].Dependencies
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
                "CrestApps.OrchardCore.ContactCenter.Routing",
                "CrestApps.OrchardCore.ContactCenter.Voice",
            ],
            dependencies);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.EntryPoints", resolverOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.EntryPoints", ingressOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.EntryPoints", navigationOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Voice", inboundServiceOwner.FeatureId);
    }

    [Fact]
    public void RecordingFeature_OwnsRecordingOrchestration()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var features = ParseManifestFeatures(repositoryRoot, ContactCenterManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var startupClasses = ParseStartupClasses(
            repositoryRoot,
            ContactCenterStartupPath,
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
            ["CrestApps.OrchardCore.ContactCenter.Voice"],
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
        var dialPadFeatures = ParseManifestFeatures(repositoryRoot, DialPadManifestPath)
            .ToDictionary(feature => feature.Id, StringComparer.Ordinal);
        var dialPadStartups = ParseStartupClassesInDirectory(
            repositoryRoot,
            DialPadModulePath,
            "CrestApps.OrchardCore.DialPad");

        // Act
        var mediaDependencies = contactCenterFeatures["CrestApps.OrchardCore.ContactCenter.Voice.Media"].Dependencies;
        var mediaResolverOwner = contactCenterStartups.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IContactCenterVoiceMediaProviderResolver, ContactCenterVoiceMediaProviderResolver>()",
                StringComparison.Ordinal));
        var asteriskBaseDependencies = asteriskFeatures["CrestApps.OrchardCore.Asterisk"].Dependencies;
        var asteriskVoiceDependencies = asteriskFeatures["CrestApps.OrchardCore.Asterisk.ContactCenterVoice"].Dependencies;
        var asteriskMediaDependencies = asteriskFeatures["CrestApps.OrchardCore.Asterisk.ContactCenterMedia"].Dependencies;
        var asteriskVoiceOwner = asteriskStartups.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IContactCenterVoiceProvider, AsteriskContactCenterVoiceProvider>()",
                StringComparison.Ordinal));
        var asteriskEventBridgeOwner = asteriskStartups.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IAsteriskRealtimeVoiceEventBridge, AsteriskContactCenterVoiceEventBridge>()",
                StringComparison.Ordinal));
        var asteriskContactCenterReconcilerOwner = asteriskStartups.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IAsteriskProviderStateReconciler, AsteriskContactCenterProviderStateReconciler>()",
                StringComparison.Ordinal));
        var asteriskMediaOwner = asteriskStartups.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IContactCenterVoiceMediaProvider, AsteriskContactCenterVoiceMediaProvider>()",
                StringComparison.Ordinal));
        var dialPadBaseDependencies = dialPadFeatures["CrestApps.OrchardCore.DialPad"].Dependencies;
        var dialPadVoiceDependencies = dialPadFeatures["CrestApps.OrchardCore.DialPad.ContactCenterVoice"].Dependencies;
        var dialPadVoiceOwner = dialPadStartups.Single(startup =>
            startup.Body.Contains(
                "AddScoped<IContactCenterVoiceProvider>(sp => sp.GetRequiredService<DialPadContactCenterVoiceProvider>())",
                StringComparison.Ordinal));
        var asteriskBaseStartup = asteriskStartups.Single(startup =>
            startup.FeatureId == "CrestApps.OrchardCore.Asterisk");

        // Assert
        Assert.Equal(["CrestApps.OrchardCore.ContactCenter.Voice"], mediaDependencies);
        Assert.Equal("CrestApps.OrchardCore.ContactCenter.Voice.Media", mediaResolverOwner.FeatureId);
        Assert.Equal(["CrestApps.OrchardCore.Telephony"], asteriskBaseDependencies);
        Assert.Equal(
            [
                "CrestApps.OrchardCore.Asterisk",
                "CrestApps.OrchardCore.ContactCenter.Voice",
            ],
            asteriskVoiceDependencies);
        Assert.Equal(
            [
                "CrestApps.OrchardCore.Asterisk.ContactCenterVoice",
                "CrestApps.OrchardCore.ContactCenter.Voice.Media",
            ],
            asteriskMediaDependencies);
        Assert.Equal("CrestApps.OrchardCore.Asterisk.ContactCenterVoice", asteriskVoiceOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.Asterisk.ContactCenterVoice", asteriskEventBridgeOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.Asterisk.ContactCenterVoice", asteriskContactCenterReconcilerOwner.FeatureId);
        Assert.Equal("CrestApps.OrchardCore.Asterisk.ContactCenterMedia", asteriskMediaOwner.FeatureId);
        Assert.DoesNotContain("IProviderVoiceEventService", asteriskBaseStartup.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("IProviderCallStateSynchronizationService", asteriskBaseStartup.Body, StringComparison.Ordinal);
        Assert.Equal(["CrestApps.OrchardCore.Telephony"], dialPadBaseDependencies);
        Assert.Equal(
            [
                "CrestApps.OrchardCore.DialPad",
                "CrestApps.OrchardCore.ContactCenter.Voice",
            ],
            dialPadVoiceDependencies);
        Assert.Equal("CrestApps.OrchardCore.DialPad.ContactCenterVoice", dialPadVoiceOwner.FeatureId);
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
            "src/Modules/CrestApps.OrchardCore.DialPad/CrestApps.OrchardCore.DialPad.csproj"));

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
            "src/Modules/CrestApps.OrchardCore.ContactCenter/Services/VoiceContactCenterCallRouter.cs",
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

    [Fact]
    public void RequiredServicesFromUndeclaredFeatures_MatchTheExpectedLedger()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var ledger = LoadLedger(repositoryRoot);
        var ownership = BuildExternalServiceOwnership(repositoryRoot);
        var graph = BuildManifestGraph(repositoryRoot);
        var contactCenterClasses = ParseStartupClassesInDirectory(
            repositoryRoot,
            ContactCenterModulePath,
            ContactCenterConstantsFeatureArea(repositoryRoot));

        var expected = ledger["knownViolations"]!.AsArray()
            .Where(entry => entry!["kind"]!.GetValue<string>() == "undeclared-required-service")
            .Select(entry => (
                Feature: entry!["feature"]!.GetValue<string>(),
                RequiredService: entry["requiredService"]!.GetValue<string>(),
                RequiredFromFeature: entry["requiredFromFeature"]!.GetValue<string>(),
                ViaType: entry["viaType"]!.GetValue<string>()))
            .ToHashSet();

        // Act
        var actual = new HashSet<(string Feature, string RequiredService, string RequiredFromFeature, string ViaType)>();

        foreach (var startupClass in contactCenterClasses)
        {
            var availableFeatures = new HashSet<string>(
                ComputeClosure(graph, startupClass.FeatureId),
                StringComparer.Ordinal)
            {
                startupClass.FeatureId,
            };

            foreach (var requiredFeatureId in startupClass.RequiredFeatureIds)
            {
                availableFeatures.Add(requiredFeatureId);
                availableFeatures.UnionWith(ComputeClosure(graph, requiredFeatureId));
            }

            foreach (var concreteType in ExtractProvidedTypes(startupClass.Body))
            {
                var parameterTypes = FindConstructorParameterTypes(repositoryRoot, concreteType);

                foreach (var parameterType in parameterTypes)
                {
                    if (!ownership.TryGetValue(parameterType, out var owningFeatures))
                    {
                        continue;
                    }

                    foreach (var owningFeature in owningFeatures)
                    {
                        if (!availableFeatures.Contains(owningFeature))
                        {
                            actual.Add((startupClass.FeatureId, parameterType, owningFeature, concreteType));
                        }
                    }
                }
            }
        }

        var undeclaredInLedger = actual.Except(expected).ToList();
        var staleInLedger = expected.Except(actual).ToList();

        // Assert
        Assert.True(
            undeclaredInLedger.Count == 0,
            "Found new, unrecorded required services resolved from an undeclared feature. Record each one in " +
            "the R0a ledger's 'knownViolations': " +
            string.Join(", ", undeclaredInLedger.Select(entry => $"{entry.Feature} requires {entry.RequiredService} from {entry.RequiredFromFeature} via {entry.ViaType}")));

        Assert.True(
            staleInLedger.Count == 0,
            "The R0a ledger records an undeclared-required-service violation that no longer reproduces; update the ledger: " +
            string.Join(", ", staleInLedger.Select(entry => $"{entry.Feature} requires {entry.RequiredService} from {entry.RequiredFromFeature} via {entry.ViaType}")));
    }

    [Fact]
    public void EveryKnownViolation_OwnsAControlMatrixGate()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var ledger = LoadLedger(repositoryRoot);
        var controlMatrix = LoadControlMatrix(repositoryRoot);
        var gateIds = controlMatrix["gates"]!.AsArray()
            .Select(gate => gate!["id"]!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);

        // Act & Assert
        foreach (var violation in ledger["knownViolations"]!.AsArray())
        {
            var violationId = violation!["id"]!.GetValue<string>();
            var gateId = violation["gateId"]!.GetValue<string>();

            Assert.True(
                gateIds.Contains(gateId),
                $"Ledger violation '{violationId}' references control-matrix gate '{gateId}', which does not exist.");
        }
    }

    [Fact]
    public void FeatureDependencyClosures_AreLegalAndMatchTheExpectedLedger()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var ledger = LoadLedger(repositoryRoot);
        var graph = BuildManifestGraph(repositoryRoot);
        var contactCenterFeatures = ParseManifestFeatures(repositoryRoot, ContactCenterManifestPath);
        var acceptedLeaves = ledger["acceptedLeafDependencies"]!.AsArray()
            .Select(entry => entry!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);
        var expectedClosures = ledger["featureDependencyClosures"]!.AsObject();

        foreach (var feature in contactCenterFeatures)
        {
            // Act
            var closure = ComputeClosure(graph, feature.Id);

            // Assert: the closure never cycles back to the feature that started it.
            Assert.DoesNotContain(feature.Id, closure);

            // Assert: every dependency is either a known feature in the parsed graph or an explicitly
            // accepted external leaf; anything else is an illegal/unresolvable manifest reference.
            foreach (var dependencyId in closure)
            {
                Assert.True(
                    graph.ContainsKey(dependencyId) || acceptedLeaves.Contains(dependencyId),
                    $"Feature '{feature.Id}' transitively depends on '{dependencyId}', which is neither a known feature nor an accepted leaf dependency in the R0a ledger.");
            }

            // Assert: the closure exactly matches the pinned characterization in the ledger.
            Assert.True(
                expectedClosures.TryGetPropertyValue(feature.Id, out var expectedClosureNode),
                $"The R0a ledger is missing an expected dependency closure for feature '{feature.Id}'.");

            var expectedClosure = expectedClosureNode!.AsArray()
                .Select(entry => entry!.GetValue<string>())
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();

            Assert.Equal(expectedClosure, closure);
        }
    }

    private static string ContactCenterConstantsFeatureArea(string repositoryRoot)
    {
        return ResolveToken(repositoryRoot, "ContactCenterConstants.Feature.Area");
    }

    private static Dictionary<string, ManifestFeature> BuildManifestGraph(string repositoryRoot)
    {
        var graph = new Dictionary<string, ManifestFeature>(StringComparer.Ordinal);

        foreach (var manifestPath in new[]
        {
            ContactCenterManifestPath,
            SignalRManifestPath,
            TelephonyManifestPath,
            OmnichannelManagementsManifestPath,
        })
        {
            foreach (var feature in ParseManifestFeatures(repositoryRoot, manifestPath))
            {
                graph[feature.Id] = feature;
            }
        }

        return graph;
    }

    private static List<string> ComputeClosure(IReadOnlyDictionary<string, ManifestFeature> graph, string featureId)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>();
        stack.Push(featureId);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            if (!graph.TryGetValue(current, out var feature))
            {
                continue;
            }

            foreach (var dependency in feature.Dependencies)
            {
                if (seen.Add(dependency))
                {
                    stack.Push(dependency);
                }
            }
        }

        return [.. seen.OrderBy(id => id, StringComparer.Ordinal)];
    }

    private static Dictionary<string, IReadOnlyList<string>> BuildExternalServiceOwnership(string repositoryRoot)
    {
        var ownership = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        var externalStartups = new[]
        {
            (Path: SignalRStartupPath, DefaultFeature: ResolveToken(repositoryRoot, "SignalRConstants.Feature.Area")),
            (Path: TelephonyStartupPath, DefaultFeature: ResolveToken(repositoryRoot, "TelephonyConstants.Feature.Area")),
            (Path: OmnichannelManagementsStartupPath, DefaultFeature: ResolveToken(repositoryRoot, "OmnichannelConstants.Features.Managements")),
        };

        foreach (var (path, defaultFeature) in externalStartups)
        {
            foreach (var startupClass in ParseStartupClasses(repositoryRoot, path, defaultFeature))
            {
                foreach (var providedType in ExtractProvidedTypes(startupClass.Body))
                {
                    if (!ownership.TryGetValue(providedType, out var owners))
                    {
                        owners = [];
                        ownership[providedType] = owners;
                    }

                    if (!owners.Contains(startupClass.FeatureId, StringComparer.Ordinal))
                    {
                        owners.Add(startupClass.FeatureId);
                    }
                }
            }
        }

        return ownership.ToDictionary(entry => entry.Key, entry => (IReadOnlyList<string>)entry.Value, StringComparer.Ordinal);
    }

    private static IReadOnlyList<string> FindConstructorParameterTypes(string repositoryRoot, string typeName)
    {
        foreach (var relativeDirectory in ContactCenterConcreteTypeSearchDirectories)
        {
            var directory = Path.Combine(repositoryRoot, relativeDirectory.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);

                if (!Regex.IsMatch(text, $@"\bclass\s+{Regex.Escape(typeName)}\b"))
                {
                    continue;
                }

                var constructorMatch = Regex.Match(text, $@"public\s+{Regex.Escape(typeName)}\s*\(");

                if (!constructorMatch.Success)
                {
                    return [];
                }

                var parenStart = constructorMatch.Index + constructorMatch.Length - 1;
                var parenEnd = FindMatching(text, parenStart, '(', ')');
                var parameterList = text.Substring(parenStart + 1, parenEnd - parenStart - 1);

                return [.. SplitTopLevel(parameterList, ',').Select(ExtractParameterTypeName)];
            }
        }

        return [];
    }

    private static string ExtractParameterTypeName(string parameterDeclaration)
    {
        var withoutDefault = parameterDeclaration.Split('=')[0].Trim();
        var tokens = withoutDefault.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // The parameter name is always the last token; the type is everything before it.
        return tokens.Length >= 2
            ? tokens[tokens.Length - 2]
            : withoutDefault;
    }

    private static List<string> ExtractProvidedTypes(string methodBody)
    {
        var provided = new List<string>();

        foreach (Match match in Regex.Matches(methodBody, @"\.Add\w*<"))
        {
            var genericStart = match.Index + match.Length - 1;
            var genericEnd = FindMatching(methodBody, genericStart, '<', '>');
            var genericArguments = SplitTopLevel(
                methodBody.Substring(genericStart + 1, genericEnd - genericStart - 1),
                ',');

            if (genericArguments.Count > 0)
            {
                provided.Add(genericArguments[genericArguments.Count - 1]);
            }
        }

        foreach (Match match in Regex.Matches(methodBody, @"\bnew\s+(?<type>\w+)\s*\("))
        {
            provided.Add(match.Groups["type"].Value);
        }

        return provided;
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

            features.Add(new ManifestFeature(id, dependencies));
            searchIndex = end + 1;
        }

        if (features.Count == 0)
        {
            // A module without a separate [assembly: Feature] block uses the Module attribute's Id as its
            // single, dependency-free feature (for example, the SignalR module).
            var moduleMatch = Regex.Match(text, @"\[assembly:\s*Module\((?<body>.*?)\)\]", RegexOptions.Singleline);
            var idMatch = Regex.Match(moduleMatch.Groups["body"].Value, @"Id\s*=\s*(?<id>[^,]+),", RegexOptions.Singleline);
            var id = ResolveToken(repositoryRoot, idMatch.Groups["id"].Value.Trim());

            features.Add(new ManifestFeature(id, []));
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
        var constantsFile = FindSourceFile(repositoryRoot, segments[0] + ".cs");
        var text = File.ReadAllText(constantsFile);
        var scope = text;

        for (var i = 1; i < segments.Length - 1; i++)
        {
            scope = ExtractNestedTypeBody(scope, segments[i]);
        }

        var propertyName = segments[segments.Length - 1];
        var match = Regex.Match(scope, $@"public const string {Regex.Escape(propertyName)}\s*=\s*""(?<value>[^""]+)"";");

        if (!match.Success)
        {
            throw new InvalidOperationException($"Could not resolve manifest token '{rawToken}' using '{constantsFile}'.");
        }

        return match.Groups["value"].Value;
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

    private static string FindSourceFile(string repositoryRoot, string fileName)
    {
        var matches = EnumerateSourceFiles(Path.Combine(repositoryRoot, "src"), fileName)
            .ToArray();

        if (matches.Length != 1)
        {
            throw new InvalidOperationException($"Expected exactly one source file named '{fileName}' under 'src', but found {matches.Length}.");
        }

        return matches[0];
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

    private static JsonObject LoadLedger(string repositoryRoot)
    {
        var ledgerPath = Path.Combine(repositoryRoot, ".github", "contact-center", "feature-dependency-violations.v1.json");

        return JsonNode.Parse(File.ReadAllText(ledgerPath))?.AsObject() ??
            throw new InvalidOperationException("The Contact Center R0a feature-dependency ledger is invalid.");
    }

    private static JsonObject LoadControlMatrix(string repositoryRoot)
    {
        var matrixPath = Path.Combine(repositoryRoot, ".github", "contact-center", "pr-test-control-matrix.v1.json");

        return JsonNode.Parse(File.ReadAllText(matrixPath))?.AsObject() ??
            throw new InvalidOperationException("The Contact Center PR-to-test control matrix is invalid.");
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

    private sealed record ManifestFeature(string Id, IReadOnlyList<string> Dependencies);

    private sealed record StartupClass(
        string FeatureId,
        string Body,
        IReadOnlyList<string> RequiredFeatureIds);
}
