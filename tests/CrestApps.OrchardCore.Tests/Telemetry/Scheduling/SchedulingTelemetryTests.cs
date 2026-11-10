using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Dispatch;
using CrestApps.OrchardCore.Dispatch.Core.Telemetry;
using CrestApps.OrchardCore.Dispatch.Models;
using CrestApps.OrchardCore.Geo.BackgroundTasks;
using CrestApps.OrchardCore.Geo.Core.Services;
using CrestApps.OrchardCore.Geo.Core.Telemetry;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.NLWeb.Models;
using CrestApps.OrchardCore.NLWeb.Services;
using CrestApps.OrchardCore.NLWeb.Telemetry;
using CrestApps.OrchardCore.Scheduling.BackgroundTasks;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.TenantHierarchy.Core.Telemetry;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.WorkOrders.Core.Telemetry;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telemetry.Scheduling;

/// <summary>
/// The scheduling, geo, dispatch, work order, tenant hierarchy and NLWeb instruments carry the names and bounded tags
/// the observability docs list, and never a tag a metric must not carry.
/// </summary>
public sealed class SchedulingTelemetryTests
{
    [Fact]
    public async Task AppointmentHoldsTask_RecordsTheRunAndTheExpiredHolds()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var appointments = new Mock<IAppointmentService>();
        appointments.Setup(service => service.ExpireHoldsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(3);
        using var services = new ServiceCollection().AddSingleton(appointments.Object).BuildServiceProvider();

        // Act
        await new AppointmentHoldsBackgroundTask().DoWorkAsync(services, TestContext.Current.CancellationToken);

        // Assert
        var runs = capture.Find("crestapps.background_task.runs", CrestAppsOrchardCoreTelemetry.TaskTag, nameof(AppointmentHoldsBackgroundTask));
        Assert.Contains(runs, run => Equals(run.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag], CrestAppsOrchardCoreTelemetry.OutcomeOk));

        var items = capture.Find("crestapps.background_task.items", CrestAppsOrchardCoreTelemetry.TaskTag, nameof(AppointmentHoldsBackgroundTask));
        Assert.Contains(items, item => Equals(item.Tags[CrestAppsOrchardCoreTelemetry.ResultTag], "expired") && item.Value == 3);
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public async Task GeocodeTask_WhenABatchIsFull_RecordsThatTheRunExceededItsBudget()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var queue = new Mock<IGeocodeQueue>();
        queue.Setup(service => service.ProcessDueAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync((int max, CancellationToken _) => max);
        using var services = new ServiceCollection().AddSingleton(queue.Object).BuildServiceProvider();

        // Act
        await new GeocodeBackgroundTask().DoWorkAsync(services, TestContext.Current.CancellationToken);

        // Assert
        var runs = capture.Find("crestapps.background_task.runs", CrestAppsOrchardCoreTelemetry.TaskTag, nameof(GeocodeBackgroundTask));
        Assert.Contains(runs, run => Equals(run.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag], BackgroundTaskTelemetry.OutcomeBudgetExceeded));
    }

    [Fact]
    public async Task GeoService_WhenTheTravelProviderFails_RecordsTheFailedCallAndTheFallback()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var providerName = "Roads" + Guid.NewGuid().ToString("n");
        var provider = new Mock<ITravelTimeProvider>();
        provider.SetupGet(p => p.Name).Returns(providerName);
        provider.Setup(p => p.GetMatrixAsync(It.IsAny<IReadOnlyList<GeoPoint>>(), It.IsAny<IReadOnlyList<GeoPoint>>(), It.IsAny<DateTime>(), It.IsAny<TravelOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));

        var settings = new Mock<IGeoSettingsService>();
        settings.Setup(s => s.GetAsync()).ReturnsAsync(new GeoSettings { TravelTimeProvider = providerName });

        var haversine = new HaversineTravelTimeProvider(settings.Object);
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var service = new DefaultGeoService([], [haversine, provider.Object], [haversine], haversine, settings.Object, cache, NullLogger<DefaultGeoService>.Instance);

        // Act
        await service.GetMatrixAsync([new GeoPoint(42.3601, -71.0589)], [new GeoPoint(42.3736, -71.1097)], DateTime.UtcNow, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var call = Assert.Single(capture.Find("crestapps.geo.provider.duration", CrestAppsOrchardCoreTelemetry.ProviderTag, providerName));
        Assert.Equal(GeoTelemetry.OperationMatrix, call.Tags[CrestAppsOrchardCoreTelemetry.OperationTag]);
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeError, call.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);

        var fallback = Assert.Single(capture.Find("crestapps.geo.provider.fallbacks", CrestAppsOrchardCoreTelemetry.ProviderTag, providerName));
        Assert.Equal(GeoTelemetry.OperationMatrix, fallback.Tags[CrestAppsOrchardCoreTelemetry.OperationTag]);

        var lookup = Assert.Single(capture.Find("crestapps.geo.matrix.cache.lookups", CrestAppsOrchardCoreTelemetry.ProviderTag, providerName));
        Assert.Equal("miss", lookup.Tags[CrestAppsOrchardCoreTelemetry.ResultTag]);
        Assert.Contains(capture.Spans, span => span.OperationName == "crestapps.geo.matrix" && Equals(span.GetTagItem(CrestAppsOrchardCoreTelemetry.ProviderTag), providerName));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public async Task ChildTenantOperation_ThatReturnsAFailure_RecordsTheFailedOutcomeWithoutTheTenant()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var entryId = "entry" + Guid.NewGuid().ToString("n");

        // Act
        var result = await TenantHierarchyTelemetry.MeasureChildOperationAsync(
            TenantHierarchyTelemetry.OperationSuspend,
            entryId,
            NullLogger.Instance,
            () => Task.FromResult(TenantHierarchyResult.Failure("Only a running child can be suspended.")));

        // Assert
        Assert.False(result.Succeeded);

        var span = Assert.Single(capture.Spans, span => Equals(span.GetTagItem(TenantHierarchyTelemetry.EntryIdTag), entryId));
        Assert.Equal("crestapps.tenanthierarchy.child.suspend", span.OperationName);
        Assert.Equal(TenantHierarchyTelemetry.OutcomeFailed, span.GetTagItem(CrestAppsOrchardCoreTelemetry.OutcomeTag));

        var durations = capture.Find("crestapps.tenanthierarchy.child.operation.duration", CrestAppsOrchardCoreTelemetry.OperationTag, TenantHierarchyTelemetry.OperationSuspend);
        Assert.Contains(durations, duration => Equals(duration.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag], TenantHierarchyTelemetry.OutcomeFailed));
        Assert.All(durations, duration => Assert.DoesNotContain(CrestAppsOrchardCoreTelemetry.TenantTag, duration.Tags.Keys));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Theory]
    [InlineData(DispatchConstants.Events.Created, "created")]
    [InlineData(DispatchConstants.Events.StatusChanged, "status_changed")]
    [InlineData(DispatchConstants.Events.AtRisk, "at_risk")]
    [InlineData("SomethingNew", "other")]
    public void DispatchEventTag_MapsEveryEventToABoundedValue(string eventName, string expected)
    {
        // Act
        var tag = DispatchTelemetry.GetEventTag(eventName);

        // Assert
        Assert.Equal(expected, tag);
    }

    [Fact]
    public void VisitAndServiceLevelEvents_AreRecordedWithBoundedTags()
    {
        // Arrange
        using var capture = new TelemetryCapture();

        // Act
        DispatchTelemetry.RecordVisitEvent(DispatchConstants.Events.Assigned, VisitStatus.Scheduled);
        WorkOrdersTelemetry.RecordSlaEvent(breached: true, "Resolution");

        // Assert
        Assert.Contains(
            capture.Find("crestapps.dispatch.visit.events", DispatchTelemetry.EventTag, "assigned"),
            measurement => Equals(measurement.Tags[DispatchTelemetry.StatusTag], nameof(VisitStatus.Scheduled)));
        Assert.Contains(
            capture.Find("crestapps.workorders.sla.events", WorkOrdersTelemetry.EventTag, WorkOrdersTelemetry.SlaBreached),
            measurement => Equals(measurement.Tags[WorkOrdersTelemetry.TargetTag], "resolution"));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public void NLWebAsk_MapsAnUnknownModeToOther_AndRecordsAnEmptyAnswer()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var request = new NLWebAskRequest();
        request.Query.Text = "opening hours";
        request.Prefer.Mode = "mode-" + Guid.NewGuid().ToString("n");

        // Act
        NLWebTelemetry.RecordAsk(new NLWebAskContext(request, null));

        // Assert
        Assert.Contains(
            capture.Find("crestapps.nlweb.ask.duration", NLWebTelemetry.ModeTag, CrestAppsOrchardCoreTelemetry.Other),
            measurement => Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag], NLWebTelemetry.OutcomeEmpty));
        Assert.DoesNotContain(capture.Measurements, measurement => measurement.Tags.Values.OfType<string>().Contains(request.Prefer.Mode));
    }
}
