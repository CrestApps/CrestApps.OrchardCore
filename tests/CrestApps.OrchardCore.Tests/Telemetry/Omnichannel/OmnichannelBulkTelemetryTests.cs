using CrestApps.Core.Services;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Telemetry;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telemetry.Omnichannel;

/// <summary>
/// Activity loads emit the <c>crestapps.omnichannel.*</c> instruments and spans the observability docs
/// list, tagged with bounded values only. The names are a public contract that dashboards and alerts depend on.
/// </summary>
public sealed class OmnichannelBulkTelemetryTests
{
    private const string TestSource = "TelemetryTestSource";

    [Fact]
    public async Task LoadAsync_RecordsTheLoadSpanAndRun_WithTheBatchIdOnTheSpanOnly()
    {
        // Arrange
        var batch = NewStartedBatch();
        var coordinator = CreateCoordinator(batch, new StubLoader(stopReason: null), new ActivityBatchLoadRegistry());

        using var capture = new TelemetryCapture();

        // Act
        await coordinator.LoadAsync(batch.ItemId, "loader", "Loader", TestContext.Current.CancellationToken);

        // Assert
        var span = Assert.Single(capture.Spans, activity =>
            activity.OperationName == "crestapps.omnichannel.batch.load" &&
            Equals(activity.GetTagItem(OmnichannelTelemetry.BatchIdSpanTag), batch.ItemId));

        Assert.Equal(OmnichannelTelemetry.SourceName, span.Source.Name);
        Assert.Equal(OmnichannelTelemetry.OutcomeCompleted, span.GetTagItem(CrestAppsOrchardCoreTelemetry.OutcomeTag));
        Assert.Equal(OmnichannelTelemetry.TriggerStart, span.GetTagItem(CrestAppsOrchardCoreTelemetry.TriggerTag));

        Assert.NotEmpty(capture.Find("crestapps.omnichannel.batch.load.duration", CrestAppsOrchardCoreTelemetry.OutcomeTag, OmnichannelTelemetry.OutcomeCompleted));
        Assert.DoesNotContain(capture.Measurements, measurement => measurement.Tags.ContainsValue(batch.ItemId));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public async Task LoadAsync_ALoadThatStopsWithAReason_IsRecordedAsStopped()
    {
        // Arrange
        var batch = NewStartedBatch();
        var coordinator = CreateCoordinator(batch, new StubLoader(stopReason: "The subject has no flow."), new ActivityBatchLoadRegistry());

        using var capture = new TelemetryCapture();

        // Act
        await coordinator.LoadAsync(batch.ItemId, "loader", "Loader", TestContext.Current.CancellationToken);

        // Assert
        var span = Assert.Single(capture.Spans, activity => Equals(activity.GetTagItem(OmnichannelTelemetry.BatchIdSpanTag), batch.ItemId));

        Assert.Equal(OmnichannelTelemetry.OutcomeStopped, span.GetTagItem(CrestAppsOrchardCoreTelemetry.OutcomeTag));
        Assert.NotEmpty(capture.Find("crestapps.omnichannel.batch.load.duration", CrestAppsOrchardCoreTelemetry.OutcomeTag, OmnichannelTelemetry.OutcomeStopped));
    }

    [Fact]
    public async Task LoadAsync_ALoadThisProcessIsAlreadyRunning_IsNotRunAndHasNoSpan()
    {
        // Arrange
        var batch = NewStartedBatch();
        var registry = new ActivityBatchLoadRegistry();
        var loader = new StubLoader(stopReason: null);
        var coordinator = CreateCoordinator(batch, loader, registry);

        Assert.True(registry.TryBegin(batch.ItemId));

        using var capture = new TelemetryCapture();

        // Act
        await coordinator.LoadAsync(batch.ItemId, "loader", "Loader", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, loader.Calls);
        Assert.DoesNotContain(capture.Spans, activity => Equals(activity.GetTagItem(OmnichannelTelemetry.BatchIdSpanTag), batch.ItemId));
    }

    [Fact]
    public void RecordLoadRecords_CountsOnlyWhatChangedSinceTheLastCommit()
    {
        // Arrange
        // Values no other test produces, so measurements from tests running in parallel cannot match them.
        var previous = new ActivityBatchLoadCounts(Loaded: 1_000, Duplicate: 0, OptedOut: 0, NoDestination: 0, NotInService: 0, LeadOrContact: 0, OverLimit: 0);
        var current = new ActivityBatchLoadCounts(Loaded: 1_000 + 7_331_017, Duplicate: 7_331_018, OptedOut: 0, NoDestination: 0, NotInService: 0, LeadOrContact: 0, OverLimit: 0);

        using var capture = new TelemetryCapture();

        // Act
        OmnichannelTelemetry.RecordLoadRecords(previous, current);

        // Assert
        Assert.Contains(capture.Find("crestapps.omnichannel.batch.load.records", CrestAppsOrchardCoreTelemetry.ResultTag, OmnichannelTelemetry.RecordLoaded), measurement => measurement.Value == 7_331_017);
        Assert.Contains(capture.Find("crestapps.omnichannel.batch.load.records", CrestAppsOrchardCoreTelemetry.ResultTag, OmnichannelTelemetry.RecordDuplicate), measurement => measurement.Value == 7_331_018);
        Assert.DoesNotContain(capture.Measurements, measurement => measurement.Instrument == "crestapps.omnichannel.batch.load.records" && measurement.Value == 1_000 + 7_331_017);
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public void ActivityBatchLoadCounts_MergesTheSkipReasonsIntoTheBoundedResults()
    {
        // Arrange
        var batch = new OmnichannelActivityBatch
        {
            TotalLoaded = 5,
            TotalSkippedAsOptedOut = 2,
            TotalSkippedAsSharedNumberOptedOut = 3,
            TotalSkippedAsConverted = 4,
            TotalSkippedAsExistingContact = 1,
            TotalSkippedByLimit = 6,
        };

        // Act
        var counts = ActivityBatchLoadCounts.Of(batch);

        // Assert
        Assert.Equal(5L, counts.Loaded);
        Assert.Equal(5L, counts.OptedOut);
        Assert.Equal(5L, counts.LeadOrContact);
        Assert.Equal(6L, counts.OverLimit);
    }

    private static OmnichannelActivityBatch NewStartedBatch()
    {
        return new OmnichannelActivityBatch
        {
            ItemId = "telemetry-" + Guid.NewGuid().ToString("n"),
            Source = TestSource,
            Status = OmnichannelActivityBatchStatus.Started,
        };
    }

    private static DefaultActivityBatchLoadCoordinator CreateCoordinator(
        OmnichannelActivityBatch batch,
        IActivityBatchLoader loader,
        ActivityBatchLoadRegistry registry)
    {
        var catalog = new Mock<ICatalog<OmnichannelActivityBatch>>();

        catalog
            .Setup(x => x.FindByIdAsync(batch.ItemId, It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(batch));

        catalog
            .Setup(x => x.UpdateAsync(It.IsAny<OmnichannelActivityBatch>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var clock = new Mock<IClock>();

        clock
            .SetupGet(x => x.UtcNow)
            .Returns(new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc));

        return new DefaultActivityBatchLoadCoordinator(
            catalog.Object,
            [loader],
            defaultLoader: null,
            NullLogger<DefaultActivityBatchLoadCoordinator>.Instance,
            clock.Object,
            registry);
    }

    private sealed class StubLoader : IActivityBatchLoader
    {
        private readonly string _stopReason;

        public StubLoader(string stopReason)
        {
            _stopReason = stopReason;
        }

        public int Calls { get; private set; }

        public string Source => TestSource;

        public Task LoadAsync(ActivityBatchLoadContext context, CancellationToken cancellationToken = default)
        {
            Calls++;
            context.StopReason = _stopReason;

            return Task.CompletedTask;
        }
    }
}
