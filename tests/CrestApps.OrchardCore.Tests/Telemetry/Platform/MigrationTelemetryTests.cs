using CrestApps.OrchardCore.Diagnostics;

namespace CrestApps.OrchardCore.Tests.Telemetry.Platform;

/// <summary>
/// Deferred data migrations record a <c>crestapps.migration.run</c> span and the
/// <c>crestapps.migration.duration</c> histogram, tagged with the migration's class name and how it ended.
/// </summary>
public sealed class MigrationTelemetryTests
{
    [Fact]
    public async Task RunAsync_RecordsTheSpanAndTheDurationOfTheMigration()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var migration = "Migrations" + Guid.NewGuid().ToString("n");
        var ran = false;

        // Act
        await MigrationTelemetry.RunAsync(migration, traceParent: null, scope: null, _ =>
        {
            ran = true;

            return Task.CompletedTask;
        });

        // Assert
        Assert.True(ran);

        var duration = Assert.Single(capture.Find("crestapps.migration.duration", MigrationTelemetry.MigrationTag, migration));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeOk, duration.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);
        Assert.Equal(CrestAppsOrchardCoreTelemetry.PlatformSourceName, duration.Meter);

        var span = Assert.Single(capture.Spans, activity => Equals(activity.GetTagItem(MigrationTelemetry.MigrationTag), migration));
        Assert.Equal("crestapps.migration.run", span.OperationName);
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public async Task RunAsync_WhenTheMigrationThrows_RecordsAnErrorAndRethrows()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var migration = "Migrations" + Guid.NewGuid().ToString("n");

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => MigrationTelemetry.RunAsync(
            migration,
            traceParent: null,
            scope: null,
            _ => throw new InvalidOperationException("boom")));

        // Assert
        var duration = Assert.Single(capture.Find("crestapps.migration.duration", MigrationTelemetry.MigrationTag, migration));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeError, duration.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);
        Assert.Equal(typeof(InvalidOperationException).FullName, duration.Tags[CrestAppsOrchardCoreTelemetry.ErrorTypeTag]);
    }
}
