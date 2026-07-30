using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Maintenance;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Maintenance;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using YesSql;
using YesSql.Provider.Sqlite;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Exercises the preview export, quiesce, reset, and verify procedure against a real SQLite-backed store so
/// the guards and the deletion behaviour are proven rather than asserted against a mock.
/// </summary>
public sealed class ContactCenterPreviewMaintenanceTests
{
    private const string TenantName = "preview";

    [Fact]
    public async Task ExportAsync_WritesEveryDocumentAndReportsCounts()
    {
        var databasePath = DatabasePath("export");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store, interactionCount: 3, queueCount: 2);

            await using var session = store.CreateSession();
            var service = CreateService(session);
            using var destination = new MemoryStream();

            var report = await service.ExportAsync(destination, TestContext.Current.CancellationToken);

            Assert.Equal(TenantName, report.TenantName);
            Assert.Equal(5, report.DocumentCount);
            Assert.Equal(3, report.DataSets.Single(dataSet => dataSet.Key == nameof(Interaction)).Count);
            Assert.Equal(2, report.DataSets.Single(dataSet => dataSet.Key == nameof(ActivityQueue)).Count);
            Assert.NotEmpty(report.Receipt);

            var json = System.Text.Encoding.UTF8.GetString(destination.ToArray());
            Assert.Contains("contact-center-preview-export/v1", json, StringComparison.Ordinal);
            Assert.Contains("interaction-0", json, StringComparison.Ordinal);
            Assert.Contains("queue-0", json, StringComparison.Ordinal);
        }
        finally
        {
            Cleanup(store, databasePath);
        }
    }

    [Fact]
    public async Task ResetAsync_WithEveryGuardSatisfied_DeletesOperationalDataAndPreservesConfiguration()
    {
        var databasePath = DatabasePath("reset-operational");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store, interactionCount: 4, queueCount: 3);

            await using var session = store.CreateSession();
            var workManager = new TestContactCenterFeatureWorkManager();
            var service = CreateService(session, workManager: workManager, allowReset: true);

            await service.QuiesceAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            using var destination = new MemoryStream();
            var export = await service.ExportAsync(destination, TestContext.Current.CancellationToken);

            var report = await service.ResetAsync(
                new ContactCenterPreviewResetRequest
                {
                    ConfirmationToken = TenantName,
                    ExportReceipt = export.Receipt,
                    Scope = ContactCenterPreviewResetScope.OperationalData,
                },
                TestContext.Current.CancellationToken);

            Assert.True(report.Succeeded, $"Expected the reset to run but it was refused because {report.RefusalReason}.");
            Assert.Equal(4, report.DeletedByDataSet[nameof(Interaction)]);
            Assert.Contains(nameof(ActivityQueue), report.PreservedDataSetKeys);

            var verification = await service.VerifyAsync(ContactCenterPreviewResetScope.OperationalData, TestContext.Current.CancellationToken);

            Assert.True(verification.IsClean, $"Residual data sets after the reset: {string.Join(", ", verification.ResidualDataSetKeys)}.");
            Assert.Equal(3, verification.DataSets.Single(dataSet => dataSet.Key == nameof(ActivityQueue)).Count);
            Assert.Equal(0, verification.DataSets.Single(dataSet => dataSet.Key == nameof(Interaction)).Count);
        }
        finally
        {
            Cleanup(store, databasePath);
        }
    }

    [Fact]
    public async Task ResetAsync_WithAllScope_AlsoDeletesConfiguration()
    {
        var databasePath = DatabasePath("reset-all");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store, interactionCount: 1, queueCount: 2);

            await using var session = store.CreateSession();
            var service = CreateService(session, allowReset: true);

            await service.QuiesceAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            using var destination = new MemoryStream();
            var export = await service.ExportAsync(destination, TestContext.Current.CancellationToken);

            var report = await service.ResetAsync(
                new ContactCenterPreviewResetRequest
                {
                    ConfirmationToken = TenantName,
                    ExportReceipt = export.Receipt,
                    Scope = ContactCenterPreviewResetScope.All,
                },
                TestContext.Current.CancellationToken);

            Assert.True(report.Succeeded, $"Expected the reset to run but it was refused because {report.RefusalReason}.");
            Assert.Empty(report.PreservedDataSetKeys);

            var verification = await service.VerifyAsync(ContactCenterPreviewResetScope.All, TestContext.Current.CancellationToken);

            Assert.True(verification.IsClean, $"Residual data sets after the reset: {string.Join(", ", verification.ResidualDataSetKeys)}.");
        }
        finally
        {
            Cleanup(store, databasePath);
        }
    }

    [Fact]
    public async Task ResetAsync_WhenResetIsNotAllowed_IsRefusedAndDeletesNothing()
    {
        await AssertRefusedAsync(
            "refusal-not-allowed",
            ContactCenterPreviewResetRefusalReason.ResetNotAllowed,
            allowReset: false,
            quiesce: true,
            supplyReceipt: true,
            confirmationToken: TenantName);
    }

    [Fact]
    public async Task ResetAsync_InProduction_IsRefusedAndDeletesNothing()
    {
        await AssertRefusedAsync(
            "refusal-production",
            ContactCenterPreviewResetRefusalReason.ProductionEnvironment,
            allowReset: true,
            quiesce: true,
            supplyReceipt: true,
            confirmationToken: TenantName,
            environmentName: Environments.Production);
    }

    [Fact]
    public async Task ResetAsync_WithMismatchedConfirmation_IsRefusedAndDeletesNothing()
    {
        await AssertRefusedAsync(
            "refusal-confirmation",
            ContactCenterPreviewResetRefusalReason.ConfirmationTokenMismatch,
            allowReset: true,
            quiesce: true,
            supplyReceipt: true,
            confirmationToken: "not-the-tenant");
    }

    [Fact]
    public async Task ResetAsync_WhenWorkAdmissionIsStillOpen_IsRefusedAndDeletesNothing()
    {
        await AssertRefusedAsync(
            "refusal-quiesce",
            ContactCenterPreviewResetRefusalReason.WorkNotQuiesced,
            allowReset: true,
            quiesce: false,
            supplyReceipt: true,
            confirmationToken: TenantName);
    }

    [Fact]
    public async Task ResetAsync_WithoutAnExportReceipt_IsRefusedAndDeletesNothing()
    {
        await AssertRefusedAsync(
            "refusal-no-receipt",
            ContactCenterPreviewResetRefusalReason.ExportReceiptMissing,
            allowReset: true,
            quiesce: true,
            supplyReceipt: false,
            confirmationToken: TenantName);
    }

    [Fact]
    public async Task ResetAsync_WhenDataChangedAfterTheExport_IsRefusedBecauseTheReceiptIsStale()
    {
        var databasePath = DatabasePath("refusal-stale");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store, interactionCount: 2, queueCount: 1);

            await using var session = store.CreateSession();
            var service = CreateService(session, allowReset: true);

            await service.QuiesceAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            using var destination = new MemoryStream();
            var export = await service.ExportAsync(destination, TestContext.Current.CancellationToken);

            // A write lands after the export was taken, so the export no longer covers everything the reset
            // would destroy.
            await SeedInteractionAsync(store, "interaction-late");

            var report = await service.ResetAsync(
                new ContactCenterPreviewResetRequest
                {
                    ConfirmationToken = TenantName,
                    ExportReceipt = export.Receipt,
                    Scope = ContactCenterPreviewResetScope.OperationalData,
                },
                TestContext.Current.CancellationToken);

            Assert.Equal(ContactCenterPreviewResetRefusalReason.ExportReceiptStale, report.RefusalReason);
            Assert.Equal(0, report.DeletedCount);

            var counts = await service.GetDataSetCountsAsync(TestContext.Current.CancellationToken);
            Assert.Equal(3, counts.Single(dataSet => dataSet.Key == nameof(Interaction)).Count);
        }
        finally
        {
            Cleanup(store, databasePath);
        }
    }

    [Fact]
    public async Task VerifyAsync_WhenOperationalDataRemains_ReportsTheResidualDataSet()
    {
        var databasePath = DatabasePath("verify-residual");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store, interactionCount: 1, queueCount: 0);

            await using var session = store.CreateSession();
            var service = CreateService(session);

            var verification = await service.VerifyAsync(ContactCenterPreviewResetScope.OperationalData, TestContext.Current.CancellationToken);

            Assert.False(verification.IsClean);
            Assert.Contains(nameof(Interaction), verification.ResidualDataSetKeys);
        }
        finally
        {
            Cleanup(store, databasePath);
        }
    }

    [Fact]
    public async Task QuiesceAsync_ClosesAdmissionForEveryParticipatingFeature()
    {
        var databasePath = DatabasePath("quiesce");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var workManager = new TestContactCenterFeatureWorkManager();
            var service = CreateService(session, workManager: workManager);

            var before = await service.GetStatusAsync(TestContext.Current.CancellationToken);
            Assert.False(before.IsQuiesced);

            var report = await service.QuiesceAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            Assert.True(report.IsDrained);
            Assert.Equal(2, report.QuiescedFeatureIds.Count);

            var after = await service.GetStatusAsync(TestContext.Current.CancellationToken);
            Assert.True(after.IsQuiesced);

            await service.ResumeAsync();

            var resumed = await service.GetStatusAsync(TestContext.Current.CancellationToken);
            Assert.False(resumed.IsQuiesced);
        }
        finally
        {
            Cleanup(store, databasePath);
        }
    }

    private static async Task AssertRefusedAsync(
        string prefix,
        ContactCenterPreviewResetRefusalReason expected,
        bool allowReset,
        bool quiesce,
        bool supplyReceipt,
        string confirmationToken,
        string environmentName = null)
    {
        var databasePath = DatabasePath(prefix);
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await SeedAsync(store, interactionCount: 2, queueCount: 1);

            await using var session = store.CreateSession();
            var service = CreateService(session, allowReset: allowReset, environmentName: environmentName);

            using var destination = new MemoryStream();
            var export = await service.ExportAsync(destination, TestContext.Current.CancellationToken);

            if (quiesce)
            {
                await service.QuiesceAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            }

            var report = await service.ResetAsync(
                new ContactCenterPreviewResetRequest
                {
                    ConfirmationToken = confirmationToken,
                    ExportReceipt = supplyReceipt ? export.Receipt : null,
                    Scope = ContactCenterPreviewResetScope.OperationalData,
                },
                TestContext.Current.CancellationToken);

            Assert.Equal(expected, report.RefusalReason);
            Assert.False(report.Succeeded);
            Assert.Equal(0, report.DeletedCount);

            var counts = await service.GetDataSetCountsAsync(TestContext.Current.CancellationToken);
            Assert.Equal(2, counts.Single(dataSet => dataSet.Key == nameof(Interaction)).Count);
            Assert.Equal(1, counts.Single(dataSet => dataSet.Key == nameof(ActivityQueue)).Count);
        }
        finally
        {
            Cleanup(store, databasePath);
        }
    }

    private static ContactCenterPreviewMaintenanceService CreateService(
        ISession session,
        TestContactCenterFeatureWorkManager workManager = null,
        bool allowReset = false,
        string environmentName = null)
    {
        var options = Options.Create(new ContactCenterPreviewMaintenanceOptions
        {
            AllowReset = allowReset,
            PageSize = 2,
        });

        var dataSets = ContactCenterPreviewDataSetRegistry.Descriptors
            .Select(descriptor => (IContactCenterPreviewDataSet)Activator.CreateInstance(
                typeof(ContactCenterPreviewDataSet<>).MakeGenericType(descriptor.DocumentType),
                session,
                descriptor.GovernanceCategoryKey,
                descriptor.IsConfiguration,
                options.Value.PageSize))
            .ToArray();

        var manager = workManager ?? new TestContactCenterFeatureWorkManager();

        var participants = new IContactCenterFeatureLifecycleParticipant[]
        {
            new TestLifecycleParticipant(ContactCenterConstants.Feature.Area, manager),
            new TestLifecycleParticipant(ContactCenterConstants.Feature.Voice, manager),
        };

        var hostEnvironment = Mock.Of<IHostEnvironment>(environment =>
            environment.EnvironmentName == (environmentName ?? Environments.Development));

        return new ContactCenterPreviewMaintenanceService(
            dataSets,
            participants,
            manager,
            new ShellSettings { Name = TenantName },
            hostEnvironment,
            session,
            Mock.Of<IClock>(clock => clock.UtcNow == new DateTime(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc)),
            options,
            NullLogger<ContactCenterPreviewMaintenanceService>.Instance);
    }

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, TestContext.Current.CancellationToken);

        return store;
    }

    private static async Task SeedAsync(IStore store, int interactionCount, int queueCount)
    {
        await using var session = store.CreateSession();

        for (var index = 0; index < interactionCount; index++)
        {
            session.Save(
                new Interaction
                {
                    ItemId = $"interaction-{index}",
                },
                collection: ContactCenterConstants.CollectionName);
        }

        for (var index = 0; index < queueCount; index++)
        {
            session.Save(
                new ActivityQueue
                {
                    ItemId = $"queue-{index}",
                    Name = $"Queue {index}",
                },
                collection: ContactCenterConstants.CollectionName);
        }

        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task SeedInteractionAsync(IStore store, string itemId)
    {
        await using var session = store.CreateSession();
        session.Save(
            new Interaction
            {
                ItemId = itemId,
            },
            collection: ContactCenterConstants.CollectionName);
        await session.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static void Cleanup(IStore store, string databasePath)
    {
        TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
    }

    private static string DatabasePath(string prefix)
        => Path.Combine(Path.GetTempPath(), $"contact-center-preview-{prefix}-{Guid.NewGuid():N}.db");

    private sealed class TestLifecycleParticipant : IContactCenterFeatureLifecycleParticipant
    {
        private readonly IContactCenterFeatureWorkManager _workManager;

        public TestLifecycleParticipant(string featureId, IContactCenterFeatureWorkManager workManager)
        {
            FeatureId = featureId;
            _workManager = workManager;
        }

        public string FeatureId { get; }

        public Task QuiesceAsync(CancellationToken cancellationToken = default)
        {
            _workManager.Quiesce(FeatureId);

            return Task.CompletedTask;
        }

        public Task DrainAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task ReconcileAsync(CancellationToken cancellationToken = default)
        {
            _workManager.Activate(FeatureId);

            return Task.CompletedTask;
        }
    }
}
