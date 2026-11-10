using System.Diagnostics;
using CrestApps.OrchardCore.BackgroundWork;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Locking;
using Moq;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;

namespace CrestApps.OrchardCore.Tests.Telemetry.Platform;

/// <summary>
/// The shared telemetry helpers emit the instruments and spans the observability docs list, and never a tag a metric
/// must not carry. The names are a public contract that dashboards and alerts depend on.
/// </summary>
public sealed class PlatformTelemetryTests
{
    [Fact]
    public async Task BackgroundTaskRun_RecordsTheOutcomeItemsAndLastSuccess()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var taskName = "Task" + Guid.NewGuid().ToString("n");

        // Act
        await BackgroundTaskTelemetry.RunAsync(taskName, run =>
        {
            run.AddItems(3);
            run.AddItems(2);
            run.AddItems(1, "failed");

            return Task.CompletedTask;
        }, TimeSpan.FromMinutes(1));

        // Assert
        var runs = Assert.Single(capture.Find("crestapps.background_task.runs", CrestAppsOrchardCoreTelemetry.TaskTag, taskName));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeOk, runs.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);

        var items = capture.Find("crestapps.background_task.items", CrestAppsOrchardCoreTelemetry.TaskTag, taskName);
        Assert.Equal(5, items.Single(item => Equals(item.Tags[CrestAppsOrchardCoreTelemetry.ResultTag], "processed")).Value);
        Assert.Equal(1, items.Single(item => Equals(item.Tags[CrestAppsOrchardCoreTelemetry.ResultTag], "failed")).Value);

        Assert.Single(capture.Find("crestapps.background_task.last_success", CrestAppsOrchardCoreTelemetry.TaskTag, taskName));
        Assert.Single(capture.Find("crestapps.background_task.lock.utilization", CrestAppsOrchardCoreTelemetry.TaskTag, taskName));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public async Task BackgroundTaskRun_WhenTheWorkThrows_RecordsAnErrorAndRethrows()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var taskName = "Task" + Guid.NewGuid().ToString("n");

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => BackgroundTaskTelemetry.RunAsync(
            taskName,
            _ => throw new InvalidOperationException("boom")));

        // Assert
        var runs = Assert.Single(capture.Find("crestapps.background_task.runs", CrestAppsOrchardCoreTelemetry.TaskTag, taskName));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeError, runs.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);
        Assert.Empty(capture.Find("crestapps.background_task.last_success", CrestAppsOrchardCoreTelemetry.TaskTag, taskName));
    }

    [Theory]
    [InlineData("lease", BackgroundTaskTelemetry.OutcomeSkippedLease)]
    [InlineData("skip", CrestAppsOrchardCoreTelemetry.OutcomeSkipped)]
    [InlineData("budget", BackgroundTaskTelemetry.OutcomeBudgetExceeded)]
    [InlineData("swallowed", CrestAppsOrchardCoreTelemetry.OutcomeError)]
    public async Task BackgroundTaskRun_RecordsTheOutcomeTheWorkReports(string report, string expectedOutcome)
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var taskName = "Task" + Guid.NewGuid().ToString("n");

        // Act
        await BackgroundTaskTelemetry.RunAsync(taskName, run =>
        {
            switch (report)
            {
                case "lease":
                    run.SkipForLease();
                    break;
                case "skip":
                    run.Skip();
                    break;
                case "budget":
                    run.BudgetExceeded();
                    break;
                default:
                    run.ReportError(new InvalidOperationException("logged and swallowed"));
                    break;
            }

            return Task.CompletedTask;
        });

        // Assert
        var runs = Assert.Single(capture.Find("crestapps.background_task.runs", CrestAppsOrchardCoreTelemetry.TaskTag, taskName));
        Assert.Equal(expectedOutcome, runs.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);

        // An idle run is healthy, so it keeps the last success fresh; a lease skip or a failure does not.
        var lastSuccess = capture.Find("crestapps.background_task.last_success", CrestAppsOrchardCoreTelemetry.TaskTag, taskName);
        Assert.Equal(expectedOutcome == CrestAppsOrchardCoreTelemetry.OutcomeSkipped ? 1 : 0, lastSuccess.Count);
    }

    [Fact]
    public void TelemetryScope_RecordsTheDurationWithTheOutcomeAndKeepsIdsOnTheSpan()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var operation = "op" + Guid.NewGuid().ToString("n");
        var histogram = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
            CrestAppsOrchardCoreTelemetry.PlatformMeter,
            "crestapps.tests.scope.duration",
            "A test duration.");

        // Act
        using (var scope = TelemetryScope.Start(
            CrestAppsOrchardCoreTelemetry.PlatformActivitySource,
            "crestapps.tests.scope",
            histogram,
            new TagList { { CrestAppsOrchardCoreTelemetry.OperationTag, operation } }))
        {
            scope.SetSpanTag("crestapps.tests.record.id", "record-1");
            scope.AddMetricTag(CrestAppsOrchardCoreTelemetry.FormatTag, "csv");
            scope.Succeed();
        }

        // Assert
        var duration = Assert.Single(capture.Find("crestapps.tests.scope.duration", CrestAppsOrchardCoreTelemetry.OperationTag, operation));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeOk, duration.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);
        Assert.Equal("csv", duration.Tags[CrestAppsOrchardCoreTelemetry.FormatTag]);
        Assert.False(duration.Tags.ContainsKey("crestapps.tests.record.id"));

        var span = Assert.Single(capture.Spans, span => Equals(span.GetTagItem(CrestAppsOrchardCoreTelemetry.OperationTag), operation));
        Assert.Equal("crestapps.tests.scope", span.OperationName);
        Assert.Equal("record-1", span.GetTagItem("crestapps.tests.record.id"));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeOk, span.GetTagItem(CrestAppsOrchardCoreTelemetry.OutcomeTag));
    }

    [Theory]
    [InlineData(typeof(InvalidOperationException), CrestAppsOrchardCoreTelemetry.OutcomeError)]
    [InlineData(typeof(OperationCanceledException), CrestAppsOrchardCoreTelemetry.OutcomeCancelled)]
    [InlineData(typeof(TimeoutException), CrestAppsOrchardCoreTelemetry.OutcomeTimeout)]
    public void TelemetryScope_Fail_MapsTheExceptionToAnOutcome(Type exceptionType, string expectedOutcome)
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var operation = "op" + Guid.NewGuid().ToString("n");
        var histogram = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
            CrestAppsOrchardCoreTelemetry.PlatformMeter,
            "crestapps.tests.scope.duration",
            "A test duration.");

        // Act
        using (var scope = TelemetryScope.Start(
            CrestAppsOrchardCoreTelemetry.PlatformActivitySource,
            "crestapps.tests.scope",
            histogram,
            new TagList { { CrestAppsOrchardCoreTelemetry.OperationTag, operation } }))
        {
            scope.Fail((Exception)Activator.CreateInstance(exceptionType));
        }

        // Assert
        var duration = Assert.Single(capture.Find("crestapps.tests.scope.duration", CrestAppsOrchardCoreTelemetry.OperationTag, operation));
        Assert.Equal(expectedOutcome, duration.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);
    }

    [Fact]
    public void TelemetryScope_DisposedWithoutAnOutcome_RecordsAnError()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var operation = "op" + Guid.NewGuid().ToString("n");
        var histogram = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
            CrestAppsOrchardCoreTelemetry.PlatformMeter,
            "crestapps.tests.scope.duration",
            "A test duration.");

        // Act
        TelemetryScope.Start(
            CrestAppsOrchardCoreTelemetry.PlatformActivitySource,
            "crestapps.tests.scope",
            histogram,
            new TagList { { CrestAppsOrchardCoreTelemetry.OperationTag, operation } }).Dispose();

        // Assert
        var duration = Assert.Single(capture.Find("crestapps.tests.scope.duration", CrestAppsOrchardCoreTelemetry.OperationTag, operation));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeError, duration.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);
    }

    [Theory]
    [InlineData(200, "2xx", CrestAppsOrchardCoreTelemetry.OutcomeOk)]
    [InlineData(429, "4xx", CrestAppsOrchardCoreTelemetry.OutcomeError)]
    [InlineData(503, "5xx", CrestAppsOrchardCoreTelemetry.OutcomeError)]
    public void ExternalCall_RecordsTheStatusClassAndOutcome(int statusCode, string expectedClass, string expectedOutcome)
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var provider = "provider" + Guid.NewGuid().ToString("n");
        var histogram = CrestAppsOrchardCoreTelemetry.CreateDurationHistogram(
            CrestAppsOrchardCoreTelemetry.PlatformMeter,
            "crestapps.tests.call.duration",
            "A test call duration.");

        // Act
        using (var scope = ExternalCallTelemetry.Start(CrestAppsOrchardCoreTelemetry.PlatformActivitySource, "crestapps.tests.call", histogram, provider, "calls.answer"))
        {
            scope.SetStatusCode(statusCode);
        }

        // Assert
        var duration = Assert.Single(capture.Find("crestapps.tests.call.duration", CrestAppsOrchardCoreTelemetry.ProviderTag, provider));
        Assert.Equal(expectedClass, duration.Tags[CrestAppsOrchardCoreTelemetry.StatusClassTag]);
        Assert.Equal(expectedOutcome, duration.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);
        Assert.Equal("calls.answer", duration.Tags[CrestAppsOrchardCoreTelemetry.OperationTag]);

        var span = Assert.Single(capture.Spans, span => Equals(span.GetTagItem(CrestAppsOrchardCoreTelemetry.ProviderTag), provider));
        Assert.Equal(ActivityKind.Client, span.Kind);
        Assert.Equal(statusCode, span.GetTagItem("http.response.status_code"));
    }

    [Fact]
    public void RetryTelemetry_CapsTheAttemptTag()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var area = "area" + Guid.NewGuid().ToString("n");

        // Act
        RetryTelemetry.RecordRetry(area, "timeout", 2);
        RetryTelemetry.RecordRetry(area, "timeout", 40);

        // Assert
        var retries = capture.Find("crestapps.retry.attempts", CrestAppsOrchardCoreTelemetry.AreaTag, area);
        Assert.Equal(2, retries.Count);
        Assert.Contains(retries, retry => Equals(retry.Tags["attempt"], 2));
        Assert.Contains(retries, retry => Equals(retry.Tags["attempt"], RetryTelemetry.MaxAttemptTag));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public async Task Locks_RecordTheResultWithTheAreaAndNeverTheKey()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var area = "tests." + Guid.NewGuid().ToString("n");
        var locker = Mock.Of<ILocker>();
        var distributedLock = new Mock<IDistributedLock>();

        distributedLock
            .Setup(service => service.TryAcquireLockAsync("free-key-123", It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync((locker, true));
        distributedLock
            .Setup(service => service.TryAcquireLockAsync("held-key-456", It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .ReturnsAsync(((ILocker)null, false));

        // Act
        await distributedLock.Object.TryAcquireLockWithoutWaitingAsync("free-key-123", area: area);
        await distributedLock.Object.TryAcquireLockWithoutWaitingAsync("held-key-456", area: area);
        await distributedLock.Object.TryAcquireMeasuredLockAsync("held-key-456", TimeSpan.FromSeconds(1), null, area);

        // Assert
        var acquisitions = capture.Find("crestapps.lock.acquisitions", LockTelemetry.AreaTag, area);
        Assert.Equal(3, acquisitions.Count);
        Assert.Contains(acquisitions, item => Equals(item.Tags[CrestAppsOrchardCoreTelemetry.ResultTag], LockTelemetry.ResultAcquired));
        Assert.Contains(acquisitions, item => Equals(item.Tags[CrestAppsOrchardCoreTelemetry.ResultTag], LockTelemetry.ResultContended));
        Assert.Contains(acquisitions, item => Equals(item.Tags[CrestAppsOrchardCoreTelemetry.ResultTag], LockTelemetry.ResultTimeout));
        Assert.Equal(3, capture.Find("crestapps.lock.wait.duration", LockTelemetry.AreaTag, area).Count);
        Assert.DoesNotContain(capture.Measurements, item => item.Tags.Values.Any(value => value is string text && text.Contains("-key-", StringComparison.Ordinal)));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public async Task Pacer_RecordsTheGateWait()
    {
        // Arrange
        using var capture = new TelemetryCapture();

        // Act: the gate is shared by the whole process, so other tests' batches may hold it; a short wait limit keeps
        // this test from waiting minutes for them, and a wait that times out is recorded too.
        await BackgroundWorkPacer.RunBatchAsync(
            _ => Task.FromResult(1),
            new BackgroundWorkPacingOptions { DatabaseShare = 1 },
            TimeSpan.FromMilliseconds(200),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(capture.Measurements, item => item.Instrument == "crestapps.background.pacer.gate_wait");
    }

    [Fact]
    public void TraceContext_LinksQueuedWorkToTheTraceThatQueuedIt()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        string traceParent;
        ActivityTraceId queuingTraceId;

        using (var queuing = CrestAppsOrchardCoreTelemetry.PlatformActivitySource.StartActivity("crestapps.tests.queue"))
        {
            queuingTraceId = queuing.TraceId;
            traceParent = TraceContextPropagation.Capture();
        }

        // Act
        using var worker = TraceContextPropagation.StartLinkedActivity(
            CrestAppsOrchardCoreTelemetry.PlatformActivitySource,
            "crestapps.tests.work",
            traceParent);

        // Assert
        Assert.NotNull(traceParent);
        Assert.NotNull(worker);
        Assert.NotEqual(queuingTraceId, worker.TraceId);
        var link = Assert.Single(worker.Links);
        Assert.Equal(queuingTraceId, link.Context.TraceId);
    }

    [Fact]
    public void TraceContext_WhenTheCurrentSpanHasEnded_StartsANewLinkedTrace()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var request = CrestAppsOrchardCoreTelemetry.PlatformActivitySource.StartActivity("crestapps.tests.request");
        var traceParent = TraceContextPropagation.Capture();

        // A detached job still sees the request span as current after the request ended.
        request.Stop();
        Activity.Current = request;

        // Act
        using var worker = TraceContextPropagation.StartLinkedActivity(
            CrestAppsOrchardCoreTelemetry.PlatformActivitySource,
            "crestapps.tests.detached",
            traceParent);

        // Assert
        Assert.NotNull(worker);
        Assert.Null(worker.Parent);
        Assert.NotEqual(request.TraceId, worker.TraceId);
        Assert.Equal(request.TraceId, Assert.Single(worker.Links).Context.TraceId);
    }

    [Fact]
    public void TraceContext_InsideTheQueuingTrace_StartsAPlainChild()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        using var request = CrestAppsOrchardCoreTelemetry.PlatformActivitySource.StartActivity("crestapps.tests.request");
        var traceParent = TraceContextPropagation.Capture();

        // Act
        using var deferred = TraceContextPropagation.StartLinkedActivity(
            CrestAppsOrchardCoreTelemetry.PlatformActivitySource,
            "crestapps.tests.deferred",
            traceParent);

        // Assert
        Assert.NotNull(deferred);
        Assert.Same(request, deferred.Parent);
        Assert.Empty(deferred.Links);
    }

    [Fact]
    public void BannedTagKeys_IncludeIdsNumbersAndUrls()
    {
        // Assert
        Assert.Contains("crestapps.report.id", CrestAppsOrchardCoreTelemetry.BannedMetricTagKeys);
        Assert.Contains("phone_number", CrestAppsOrchardCoreTelemetry.BannedMetricTagKeys);
        Assert.Contains("url", CrestAppsOrchardCoreTelemetry.BannedMetricTagKeys);
        Assert.Contains("lock.key", CrestAppsOrchardCoreTelemetry.BannedMetricTagKeys);
        Assert.DoesNotContain(CrestAppsOrchardCoreTelemetry.OutcomeTag, CrestAppsOrchardCoreTelemetry.BannedMetricTagKeys);
        Assert.DoesNotContain(CrestAppsOrchardCoreTelemetry.ProviderTag, CrestAppsOrchardCoreTelemetry.BannedMetricTagKeys);
    }

    [Theory]
    [InlineData(null, "none")]
    [InlineData(101, "1xx")]
    [InlineData(204, "2xx")]
    [InlineData(302, "3xx")]
    [InlineData(404, "4xx")]
    [InlineData(500, "5xx")]
    public void GetStatusClass_ReturnsTheClass(int? statusCode, string expected)
    {
        // Act & Assert
        Assert.Equal(expected, ExternalCallTelemetry.GetStatusClass(statusCode));
    }
}
