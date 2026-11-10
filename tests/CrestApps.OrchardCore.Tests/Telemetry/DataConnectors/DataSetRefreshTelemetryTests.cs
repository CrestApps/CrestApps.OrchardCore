using System.Diagnostics;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Services;
using CrestApps.OrchardCore.DataConnectors.Telemetry;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Tests.DataConnectors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore;
using OrchardCore.DataSources;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Telemetry.DataConnectors;

/// <summary>
/// Refreshes and live reads of connected data sets emit the spans and metrics the observability docs list, and the
/// refresh sweep goes on past a refresh that throws. The names are a public contract that dashboards and alerts depend
/// on. Metrics never carry a data set id, so a test finds its own refresh by the id on the span and only checks that
/// the metrics it expects were recorded with the expected bounded tags.
/// </summary>
public sealed class DataSetRefreshTelemetryTests
{
    [Fact]
    public async Task RefreshAsync_RecordsTheRefreshSpanItsPhasesAndTheRefreshMetrics()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        await using var harness = await DataSetRefreshServiceTests.Harness.CreateAsync();
        harness.Connector.Rows = [DataConnectorTestData.Row(1, "Ada", 1), DataConnectorTestData.Row(2, "Grace", 2)];

        var dataSet = DataConnectorTestData.DataSet();
        dataSet.ItemId = NewId();
        dataSet = await harness.AddDataSetAsync(dataSet);

        // Act
        var run = await harness.RefreshAsync(dataSet);

        // Assert
        Assert.Equal(DataSetRefreshStatus.Succeeded, run.Status);

        var span = Assert.Single(FindRefreshSpans(capture, dataSet.ItemId));
        Assert.Equal(run.ItemId, span.GetTagItem(DataConnectorsTelemetry.RefreshIdSpanTag));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeOk, span.GetTagItem(CrestAppsOrchardCoreTelemetry.OutcomeTag));

        var phases = capture.Spans
            .Where(phase => phase.ParentSpanId == span.SpanId)
            .Select(phase => phase.OperationName)
            .ToList();

        Assert.Contains("crestapps.dataconnector.refresh.describe", phases);
        Assert.Contains("crestapps.dataconnector.refresh.batches", phases);
        Assert.Contains("crestapps.dataconnector.refresh.count", phases);

        Assert.Contains(capture.Measurements, measurement => measurement.Instrument == "crestapps.dataconnector.refreshes" && HasRefreshTags(measurement, CrestAppsOrchardCoreTelemetry.OutcomeOk));
        Assert.Contains(capture.Measurements, measurement => measurement.Instrument == "crestapps.dataconnector.refresh.duration" && HasRefreshTags(measurement, CrestAppsOrchardCoreTelemetry.OutcomeOk));
        Assert.Contains(capture.Measurements, measurement => measurement.Instrument == "crestapps.dataconnector.refresh.rows" && measurement.Value == 2 && Equals(measurement.Tags[DataConnectorsTelemetry.ConnectorTag], TestDataConnector.Name));
        Assert.Contains(capture.Measurements, measurement => measurement.Instrument == "crestapps.dataconnector.refresh.active" && measurement.Value == 1);
        Assert.Contains(capture.Measurements, measurement => measurement.Instrument == "crestapps.dataconnector.refresh.active" && measurement.Value == -1);
        Assert.Contains(capture.Measurements, measurement => measurement.Instrument == "crestapps.dataconnector.rowstore.write.duration");
        Assert.Empty(FindBannedTagKeys(capture));
    }

    [Fact]
    public async Task ReadAsync_RecordsTheReadSpanItsRowsAndItsDuration()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        await using var harness = await DataSetRefreshServiceTests.Harness.CreateAsync();
        harness.Connector.Rows = [DataConnectorTestData.Row(1, "Ada", 1), DataConnectorTestData.Row(2, "Grace", 2), DataConnectorTestData.Row(3, "Linus", 3)];

        var dataSet = DataConnectorTestData.DataSet(DataSetAccessMode.Live);
        dataSet.ItemId = NewId();
        var rows = 0;

        // Act
        await harness.InScopeAsync(async provider =>
        {
            var reader = provider.GetRequiredService<ConnectedDataReader>();

            await foreach (var batch in reader.ReadAsync(dataSet, new DataSourceQuery { DataSet = dataSet.Name }, DataConnectorReadPurpose.Live, TestContext.Current.CancellationToken))
            {
                rows += batch.Count;
            }
        });

        // Assert
        Assert.Equal(3, rows);

        var span = Assert.Single(capture.Spans, read => read.OperationName == "crestapps.dataconnector.read" && Equals(read.GetTagItem(DataConnectorsTelemetry.DataSetIdSpanTag), dataSet.ItemId));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeOk, span.GetTagItem(CrestAppsOrchardCoreTelemetry.OutcomeTag));
        Assert.Equal(3L, span.GetTagItem(DataConnectorsTelemetry.RowsSpanTag));

        Assert.Contains(capture.Measurements, measurement =>
            measurement.Instrument == "crestapps.dataconnector.read.duration" &&
            Equals(measurement.Tags[DataConnectorsTelemetry.PurposeTag], "live") &&
            Equals(measurement.Tags[DataConnectorsTelemetry.ConnectorTag], TestDataConnector.Name) &&
            Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag], CrestAppsOrchardCoreTelemetry.OutcomeOk));
        Assert.Contains(capture.Measurements, measurement => measurement.Instrument == "crestapps.dataconnector.read.rows" && measurement.Value == 3);
        Assert.Empty(FindBannedTagKeys(capture));
    }

    [Fact]
    public async Task RefreshDueAsync_GoesOnAfterAFailedRefresh_ButStopsWithoutThrowingWhenARefreshCannotRecordItsEnd()
    {
        // Arrange
        using var capture = new TelemetryCapture();

        // A data set without a connection fails its refresh the ordinary way, with a status.
        var failing = DataConnectorTestData.DataSet(name: "Failing");
        failing.ItemId = NewId();
        failing.ConnectionId = null;

        // Saving the refresh of this one throws, which cancels the session every refresh of the run shares.
        var broken = DataConnectorTestData.DataSet(name: "Broken");
        broken.ItemId = NewId();

        var later = DataConnectorTestData.DataSet(name: "Later");
        later.ItemId = NewId();

        var dataSetStore = new Mock<IConnectedDataSetStore>();
        dataSetStore
            .Setup(store => store.ListDueAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConnectedDataSet> { failing, broken, later });
        dataSetStore
            .Setup(store => store.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConnectedDataSet>());

        var runStore = new Mock<IDataSetRefreshRunStore>();
        runStore
            .Setup(store => store.CreateAsync(It.Is<DataSetRefreshRun>(run => run.DataSetId == broken.ItemId), It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromException(new InvalidOperationException("The database went away.")));

        var service = CreateService(dataSetStore, runStore, new InMemoryDistributedLock());

        // Act
        var count = await service.RefreshDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, count);
        runStore.Verify(store => store.CreateAsync(It.Is<DataSetRefreshRun>(run => run.DataSetId == failing.ItemId), It.IsAny<CancellationToken>()), Times.Once());
        runStore.Verify(store => store.CreateAsync(It.Is<DataSetRefreshRun>(run => run.DataSetId == later.ItemId), It.IsAny<CancellationToken>()), Times.Never());

        var failingSpan = Assert.Single(FindRefreshSpans(capture, failing.ItemId));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeError, failingSpan.GetTagItem(CrestAppsOrchardCoreTelemetry.OutcomeTag));

        var brokenSpan = Assert.Single(FindRefreshSpans(capture, broken.ItemId));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeError, brokenSpan.GetTagItem(CrestAppsOrchardCoreTelemetry.OutcomeTag));
        Assert.Empty(FindRefreshSpans(capture, later.ItemId));
        Assert.Empty(FindBannedTagKeys(capture));
    }

    [Fact]
    public async Task RefreshDueAsync_DoesNotCountARefreshSkippedBecauseItsDataSetIsLocked_AndRecordsItAsSkipped()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var locked = DataConnectorTestData.DataSet(name: "Locked");
        locked.ItemId = NewId();

        var dataSetStore = new Mock<IConnectedDataSetStore>();
        dataSetStore
            .Setup(store => store.ListDueAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConnectedDataSet> { locked });
        dataSetStore
            .Setup(store => store.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConnectedDataSet>());

        var runStore = new Mock<IDataSetRefreshRunStore>();
        var distributedLock = new InMemoryDistributedLock();
        var (locker, acquired) = await distributedLock.TryAcquireLockAsync($"CrestApps.DataConnectors.Refresh:{locked.ItemId}", TimeSpan.Zero);
        var service = CreateService(dataSetStore, runStore, distributedLock);

        // Act
        var count = await service.RefreshDueAsync(TestContext.Current.CancellationToken);
        await locker.DisposeAsync();

        // Assert
        Assert.True(acquired);
        Assert.Equal(0, count);
        runStore.Verify(store => store.CreateAsync(It.IsAny<DataSetRefreshRun>(), It.IsAny<CancellationToken>()), Times.Never());

        var span = Assert.Single(FindRefreshSpans(capture, locked.ItemId));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeSkipped, span.GetTagItem(CrestAppsOrchardCoreTelemetry.OutcomeTag));
        Assert.Contains(capture.Measurements, measurement => measurement.Instrument == "crestapps.dataconnector.refreshes" && Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag], CrestAppsOrchardCoreTelemetry.OutcomeSkipped));
        Assert.Contains(capture.Measurements, measurement =>
            measurement.Instrument == "crestapps.lock.acquisitions" &&
            Equals(measurement.Tags.GetValueOrDefault("lock.area"), "dataconnectors.refresh") &&
            Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.ResultTag], "contended"));
    }

    private static DataSetRefreshService CreateService(
        Mock<IConnectedDataSetStore> dataSetStore,
        Mock<IDataSetRefreshRunStore> runStore,
        InMemoryDistributedLock distributedLock)
    {
        var options = Options.Create(new DataConnectorsOptions());
        var reader = new ConnectedDataReader(
            Mock.Of<IDataConnectionStore>(),
            Mock.Of<IDataConnectorManager>(),
            Mock.Of<IDataConnectionSecretManager>(),
            options,
            NullLogger<ConnectedDataReader>.Instance);

        return new DataSetRefreshService(
            Mock.Of<ISession>(),
            dataSetStore.Object,
            runStore.Object,
            Mock.Of<IDataSetRowStore>(),
            reader,
            distributedLock,
            new FixedClock(),
            options,
            NullLogger<DataSetRefreshService>.Instance);
    }

    private static List<Activity> FindRefreshSpans(TelemetryCapture capture, string dataSetId)
    {
        return capture.Spans
            .Where(span => span.OperationName == "crestapps.dataconnector.refresh" && Equals(span.GetTagItem(DataConnectorsTelemetry.DataSetIdSpanTag), dataSetId))
            .ToList();
    }

    private static bool HasRefreshTags(CapturedMeasurement measurement, string outcome)
    {
        return Equals(measurement.Tags.GetValueOrDefault(CrestAppsOrchardCoreTelemetry.OutcomeTag), outcome) &&
            Equals(measurement.Tags.GetValueOrDefault(CrestAppsOrchardCoreTelemetry.TriggerTag), "manual") &&
            Equals(measurement.Tags.GetValueOrDefault(DataConnectorsTelemetry.ModeTag), "full") &&
            Equals(measurement.Tags.GetValueOrDefault(DataConnectorsTelemetry.ConnectorTag), TestDataConnector.Name);
    }

    // Only this area's meter is checked, so a banned tag another area records doesn't fail these tests.
    private static List<string> FindBannedTagKeys(TelemetryCapture capture)
    {
        return capture.Measurements
            .Where(measurement => measurement.Meter == DataConnectorsTelemetry.SourceName)
            .SelectMany(measurement => measurement.Tags.Keys)
            .Where(CrestAppsOrchardCoreTelemetry.BannedMetricTagKeys.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string NewId()
    {
        return IdGenerator.GenerateId();
    }
}
