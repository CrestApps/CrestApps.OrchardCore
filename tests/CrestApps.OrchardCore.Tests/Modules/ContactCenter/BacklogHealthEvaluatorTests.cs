using CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class BacklogHealthEvaluatorTests
{
    private static ContactCenterHealthCheckOptions Options()
    {
        return new ContactCenterHealthCheckOptions
        {
            DeadLetterDegradedThreshold = 1,
            DeadLetterUnhealthyThreshold = 25,
            OverdueBacklogDegradedThreshold = 50,
            OverdueBacklogUnhealthyThreshold = 500,
        };
    }

    [Fact]
    public void Evaluate_WhenBelowAllThresholds_ReportsHealthy()
    {
        // Act
        var result = BacklogHealthEvaluator.Evaluate("outbox", deadLetterCount: 0, overdueCount: 10, Options());

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(0, result.Data["deadLettered"]);
        Assert.Equal(10, result.Data["overdue"]);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 50)]
    [InlineData(24, 499)]
    public void Evaluate_WhenAtOrAboveDegradedButBelowUnhealthy_ReportsDegraded(int deadLettered, int overdue)
    {
        // Act
        var result = BacklogHealthEvaluator.Evaluate("outbox", deadLettered, overdue, Options());

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Theory]
    [InlineData(25, 0)]
    [InlineData(0, 500)]
    [InlineData(1000, 1000)]
    public void Evaluate_WhenAtOrAboveUnhealthy_ReportsUnhealthy(int deadLettered, int overdue)
    {
        // Act
        var result = BacklogHealthEvaluator.Evaluate("outbox", deadLettered, overdue, Options());

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public void Evaluate_WhenUnhealthyThresholdBelowDegraded_NormalizesToDegradedFloor()
    {
        // Arrange
        var options = new ContactCenterHealthCheckOptions
        {
            DeadLetterDegradedThreshold = 10,
            DeadLetterUnhealthyThreshold = 2,
            OverdueBacklogDegradedThreshold = 10,
            OverdueBacklogUnhealthyThreshold = 2,
        };

        // Act
        var result = BacklogHealthEvaluator.Evaluate("outbox", deadLetterCount: 10, overdueCount: 0, options);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(10, options.DeadLetterUnhealthyThreshold);
    }
}
