using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.BackgroundTasks;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Extensions.Features;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterFeatureLifecycleTests
{
    [Fact]
    public void LifecycleContract_EnumeratesFeatureOwnedWorkAndRecordsR3ActiveDrainCoverage()
    {
        // Arrange
        var contract = LoadLifecycleContract();
        var entries = contract["entries"]?.AsArray();

        // Act
        var featureIds = entries
            .Select(entry => entry?["featureId"]?.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);
        var componentIds = entries
            .Select(entry => entry?["component"]?.GetValue<string>())
            .ToList();
        var implementedComponents = new HashSet<string>(StringComparer.Ordinal)
        {
            "OutboxDispatchBackgroundTask",
            "CallbackDispatchBackgroundTask",
            "DialerPacingBackgroundTask",
            "ProviderWebhookInboxBackgroundTask",
            "ProviderCallStateReconciliationBackgroundTask",
            "ProviderCommandRecoveryBackgroundTask",
            "ProviderVoiceWebhookEndpoint",
            "ContactCenterVoiceTenantEvents",
            "ContactCenterHub",
            "AsteriskContactCenterVoiceProvider",
            "AsteriskContactCenterVoiceMediaProvider",
            "DialPadContactCenterVoiceProvider",
        };

        // Assert
        Assert.Equal("1.0", contract["version"]?.GetValue<string>());
        Assert.NotEmpty(entries);
        Assert.Equal(componentIds.Count, componentIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(ContactCenterConstants.Feature.Voice, featureIds);
        Assert.Contains(ContactCenterConstants.Feature.RealTime, featureIds);
        Assert.Contains(ContactCenterConstants.Feature.Dialer, featureIds);
        Assert.Contains("CrestApps.OrchardCore.Asterisk.ContactCenterVoice", featureIds);
        Assert.Contains("CrestApps.OrchardCore.Asterisk.ContactCenterMedia", featureIds);
        Assert.Contains("CrestApps.OrchardCore.DialPad.ContactCenterVoice", featureIds);

        foreach (var entry in entries)
        {
            Assert.False(string.IsNullOrWhiteSpace(entry?["featureId"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(entry?["component"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(entry?["quiesce"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(entry?["drain"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(entry?["reEnable"]?.GetValue<string>()));
            Assert.Equal("verified-idle", entry?["idleStatus"]?.GetValue<string>());
            var expectedStatus = implementedComponents.Contains(entry?["component"]?.GetValue<string>())
                ? "implemented-r3"
                : "deferred-follow-up";

            Assert.Equal(expectedStatus, entry?["activeWorkStatus"]?.GetValue<string>());
        }
    }

    [Fact]
    public void LifecycleContract_TracksEveryFeatureOwnedBackgroundTask()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var contractComponents = LoadLifecycleContract()["entries"]?.AsArray()
            .Select(entry => entry?["component"]?.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);
        var sourceRoots = new[]
        {
            "src/Modules/CrestApps.OrchardCore.ContactCenter",
            "src/Modules/CrestApps.OrchardCore.Telephony",
            "src/Modules/CrestApps.OrchardCore.Omnichannel.Managements",
        };

        // Act
        var backgroundTasks = sourceRoots
            .SelectMany(path => Directory.EnumerateFiles(Path.Combine(repositoryRoot, path), "*.cs", SearchOption.AllDirectories))
            .Where(path => File.ReadAllText(path).Contains(": IBackgroundTask", StringComparison.Ordinal))
            .Select(Path.GetFileNameWithoutExtension)
            .ToList();

        // Assert
        Assert.NotEmpty(backgroundTasks);

        foreach (var backgroundTask in backgroundTasks)
        {
            Assert.Contains(backgroundTask, contractComponents);
        }
    }

    [Fact]
    public void Startup_RegistersPreDisableLifecycleHook()
    {
        // Arrange
        var repositoryRoot = FindRepositoryRoot();
        var startup = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "Modules",
            "CrestApps.OrchardCore.ContactCenter",
            "Startup.cs"));

        // Act & Assert
        Assert.Contains(
            ".AddScoped<IFeatureEventHandler, ContactCenterFeatureLifecycleHandler>()",
            startup,
            StringComparison.Ordinal);
        Assert.Contains(
            ".AddScoped<IContactCenterFeatureLifecycleParticipant>",
            startup,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task QuiesceAsync_TargetFeature_QuiescesAllParticipantsBeforeDraining()
    {
        // Arrange
        var operations = new List<string>();
        var coordinator = new ContactCenterFeatureLifecycleCoordinator(
        [
            new TestFeatureLifecycleParticipant("feature-a", "first", operations),
            new TestFeatureLifecycleParticipant("feature-b", "ignored", operations),
            new TestFeatureLifecycleParticipant("feature-a", "second", operations),
        ],
            NullLogger<ContactCenterFeatureLifecycleCoordinator>.Instance);

        // Act
        await coordinator.QuiesceAsync("feature-a", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
        [
            "quiesce:first",
            "quiesce:second",
            "drain:first",
            "drain:second",
        ],
            operations);
    }

    [Fact]
    public async Task ReconcileAsync_TargetFeature_ReconcilesMatchingParticipants()
    {
        // Arrange
        var operations = new List<string>();
        var coordinator = new ContactCenterFeatureLifecycleCoordinator(
        [
            new TestFeatureLifecycleParticipant("feature-a", "first", operations),
            new TestFeatureLifecycleParticipant("feature-b", "second", operations),
        ],
            NullLogger<ContactCenterFeatureLifecycleCoordinator>.Instance);

        // Act
        await coordinator.ReconcileAsync("feature-a", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
        [
            "reconcile:first",
        ],
            operations);
    }

    [Fact]
    public async Task QuiesceAsync_ParticipantFails_ContinuesQuiescingAndDrainingPeers()
    {
        // Arrange
        var operations = new List<string>();
        var coordinator = new ContactCenterFeatureLifecycleCoordinator(
        [
            new ThrowingFeatureLifecycleParticipant("feature-a", "failing", operations, throwOnQuiesce: true),
            new TestFeatureLifecycleParticipant("feature-a", "healthy", operations),
        ],
            NullLogger<ContactCenterFeatureLifecycleCoordinator>.Instance);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<AggregateException>(() =>
            coordinator.QuiesceAsync("feature-a", TestContext.Current.CancellationToken));

        Assert.Single(exception.InnerExceptions);
        Assert.Equal(
        [
            "quiesce:failing",
            "quiesce:healthy",
        ],
            operations);
    }

    [Fact]
    public async Task WorkManager_QuiesceRejectsNewWorkAndDrainWaitsForAdmittedWork()
    {
        // Arrange
        var manager = new ContactCenterFeatureWorkManager();
        var lease = manager.TryEnter("feature-a");
        manager.Quiesce("feature-a");

        // Act
        var drain = manager.DrainAsync(
            "feature-a",
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(lease);
        Assert.Null(manager.TryEnter("feature-a"));
        Assert.False(drain.IsCompleted);

        lease.Dispose();
        await drain;
    }

    [Fact]
    public async Task WorkManager_DrainTimeoutFailsClosedUntilActivated()
    {
        // Arrange
        var manager = new ContactCenterFeatureWorkManager();
        using var lease = manager.TryEnter("feature-a");
        manager.Quiesce("feature-a");

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(() =>
            manager.DrainAsync(
                "feature-a",
                TimeSpan.FromMilliseconds(20),
                TestContext.Current.CancellationToken));
        Assert.Null(manager.TryEnter("feature-a"));

        manager.Activate("feature-a");

        Assert.NotNull(manager.TryEnter("feature-a"));
    }

    [Fact]
    public async Task DisablingAsync_WhenQuiesceFails_DoesNotReopenAdmissionDuringTeardown()
    {
        // Arrange
        var operations = new List<string>();
        var coordinator = new ContactCenterFeatureLifecycleCoordinator(
        [
            new ThrowingFeatureLifecycleParticipant(
                "feature-a",
                "participant",
                operations,
                throwOnQuiesce: true),
        ],
            NullLogger<ContactCenterFeatureLifecycleCoordinator>.Instance);
        var services = new ServiceCollection()
            .AddSingleton(coordinator)
            .BuildServiceProvider();
        var handler = new ContactCenterFeatureLifecycleHandler(services);
        var feature = new Mock<IFeatureInfo>();
        feature.SetupGet(value => value.Id).Returns("feature-a");

        // Act & Assert
        await Assert.ThrowsAsync<AggregateException>(() => handler.DisablingAsync(feature.Object));
        Assert.Equal(
        [
            "quiesce:participant",
        ],
            operations);
    }

    [Fact]
    public async Task WorkLifecycleParticipant_DrainTimeoutPropagates()
    {
        // Arrange
        var manager = new ContactCenterFeatureWorkManager();
        using var lease = manager.TryEnter("feature-a");
        var participant = new ContactCenterFeatureWorkLifecycleParticipant(
            "feature-a",
            manager,
            Options.Create(new ContactCenterFeatureLifecycleOptions
            {
                DrainTimeoutSeconds = 1,
            }));
        await participant.QuiesceAsync(TestContext.Current.CancellationToken);

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(() =>
            participant.DrainAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RealTimeParticipant_QuiesceAbortsConnectionsAndRejectsNewRegistrations()
    {
        // Arrange
        var manager = new ContactCenterFeatureWorkManager();
        var registry = new ContactCenterHubConnectionRegistry();
        var participant = new ContactCenterRealTimeLifecycleParticipant(
            manager,
            registry,
            Options.Create(new ContactCenterFeatureLifecycleOptions
            {
                DrainTimeoutSeconds = 1,
            }));
        var activeConnection = new Mock<HubCallerContext>();
        activeConnection.SetupGet(context => context.ConnectionId).Returns("connection-1");
        var rejectedConnection = new Mock<HubCallerContext>();
        rejectedConnection.SetupGet(context => context.ConnectionId).Returns("connection-2");
        Assert.True(registry.Register(activeConnection.Object));

        // Act
        await participant.QuiesceAsync(TestContext.Current.CancellationToken);
        var registered = registry.Register(rejectedConnection.Object);

        // Assert
        Assert.False(registered);
        Assert.Null(manager.TryEnter(ContactCenterConstants.Feature.RealTime));
        activeConnection.Verify(context => context.Abort(), Times.Once);
        rejectedConnection.Verify(context => context.Abort(), Times.Once);

        await participant.ReconcileAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(manager.TryEnter(ContactCenterConstants.Feature.RealTime));
    }

    [Fact]
    public async Task ReconcileAsync_ParticipantFails_ContinuesReconcilingPeers()
    {
        // Arrange
        var operations = new List<string>();
        var coordinator = new ContactCenterFeatureLifecycleCoordinator(
        [
            new ThrowingFeatureLifecycleParticipant("feature-a", "failing", operations, throwOnReconcile: true),
            new TestFeatureLifecycleParticipant("feature-a", "healthy", operations),
        ],
            NullLogger<ContactCenterFeatureLifecycleCoordinator>.Instance);

        // Act
        await coordinator.ReconcileAsync("feature-a", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
        [
            "reconcile:failing",
            "reconcile:healthy",
        ],
            operations);
    }

    [Fact]
    public async Task DisablingAsync_FeatureEvent_QuiescesMatchingFeature()
    {
        // Arrange
        var operations = new List<string>();
        var coordinator = new ContactCenterFeatureLifecycleCoordinator(
        [
            new TestFeatureLifecycleParticipant("feature-a", "participant", operations),
        ],
            NullLogger<ContactCenterFeatureLifecycleCoordinator>.Instance);
        var services = new ServiceCollection()
            .AddSingleton(coordinator)
            .BuildServiceProvider();
        var handler = new ContactCenterFeatureLifecycleHandler(services);
        var feature = new Mock<IFeatureInfo>();
        feature.SetupGet(value => value.Id).Returns("feature-a");

        // Act
        await handler.DisablingAsync(feature.Object);

        // Assert
        Assert.Equal(
        [
            "quiesce:participant",
            "drain:participant",
        ],
            operations);
    }

    [Fact]
    public async Task ReconciliationBackgroundTask_FreshScope_ReconcilesVoiceParticipants()
    {
        // Arrange
        var synchronizationService = new Mock<IProviderCallStateSynchronizationService>();
        var workManager = new TestContactCenterFeatureWorkManager();
        var tenantEvents = new ContactCenterVoiceTenantEvents(
            synchronizationService.Object,
            workManager,
            Options.Create(new ContactCenterFeatureLifecycleOptions()),
            NullLogger<ContactCenterVoiceTenantEvents>.Instance);
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IContactCenterFeatureWorkManager>(workManager)
            .AddSingleton(tenantEvents)
            .BuildServiceProvider();
        var task = new ProviderCallStateReconciliationBackgroundTask();

        // Act
        await task.DoWorkAsync(services, TestContext.Current.CancellationToken);

        // Assert
        synchronizationService.Verify(
            service => service.ReconcileActiveInteractionsAsync(TestContext.Current.CancellationToken),
            Times.Once);
    }

    private sealed class TestFeatureLifecycleParticipant : IContactCenterFeatureLifecycleParticipant
    {
        private readonly string _name;
        private readonly List<string> _operations;

        public TestFeatureLifecycleParticipant(
            string featureId,
            string name,
            List<string> operations)
        {
            FeatureId = featureId;
            _name = name;
            _operations = operations;
        }

        public string FeatureId { get; }

        public Task QuiesceAsync(CancellationToken cancellationToken = default)
        {
            _operations.Add($"quiesce:{_name}");

            return Task.CompletedTask;
        }

        public Task DrainAsync(CancellationToken cancellationToken = default)
        {
            _operations.Add($"drain:{_name}");

            return Task.CompletedTask;
        }

        public Task ReconcileAsync(CancellationToken cancellationToken = default)
        {
            _operations.Add($"reconcile:{_name}");

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingFeatureLifecycleParticipant : IContactCenterFeatureLifecycleParticipant
    {
        private readonly string _name;
        private readonly List<string> _operations;
        private readonly bool _throwOnQuiesce;
        private readonly bool _throwOnReconcile;

        public ThrowingFeatureLifecycleParticipant(
            string featureId,
            string name,
            List<string> operations,
            bool throwOnQuiesce = false,
            bool throwOnReconcile = false)
        {
            FeatureId = featureId;
            _name = name;
            _operations = operations;
            _throwOnQuiesce = throwOnQuiesce;
            _throwOnReconcile = throwOnReconcile;
        }

        public string FeatureId { get; }

        public Task QuiesceAsync(CancellationToken cancellationToken = default)
        {
            _operations.Add($"quiesce:{_name}");

            return _throwOnQuiesce
                ? Task.FromException(new InvalidOperationException("Expected test failure."))
                : Task.CompletedTask;
        }

        public Task DrainAsync(CancellationToken cancellationToken = default)
        {
            _operations.Add($"drain:{_name}");

            return Task.CompletedTask;
        }

        public Task ReconcileAsync(CancellationToken cancellationToken = default)
        {
            _operations.Add($"reconcile:{_name}");

            return _throwOnReconcile
                ? Task.FromException(new InvalidOperationException("Expected test failure."))
                : Task.CompletedTask;
        }
    }

    private static JsonObject LoadLifecycleContract()
    {
        var repositoryRoot = FindRepositoryRoot();
        var contractPath = Path.Combine(
            repositoryRoot,
            ".github",
            "contact-center",
            "feature-lifecycle-contracts.v1.json");

        return JsonNode.Parse(File.ReadAllText(contractPath))?.AsObject() ??
            throw new InvalidOperationException("The Contact Center feature lifecycle contract is invalid.");
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
}
