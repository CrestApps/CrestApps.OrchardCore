using CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Verifies that a deployment which does not satisfy its declared topology refuses traffic.
/// </summary>
/// <remarks>
/// Detecting an unsupported topology only matters if something acts on the detection. These tests pin the two
/// consequences: the node reports unready, and the shared admission gate refuses to hand out work.
/// </remarks>
public sealed class ContactCenterTopologyHealthCheckTests
{
    [Fact]
    public void Evaluate_ReportsUnhealthy_BeforeValidationHasRun()
    {
        // Starting healthy and tightening later would open a window in which an unsupported deployment accepts
        // traffic, and that window is precisely when a shell is being reloaded.
        var result = ContactCenterTopologyHealthCheck.Evaluate(CreateContext(), result: null);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("not been validated", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ReportsUnhealthy_AndSurfacesEveryFailure_WhenTheTopologyIsNotSatisfied()
    {
        var verdict = new ContactCenterTopologyValidationResult
        {
            DeclaredProfileId = ContactCenterTopologyProfiles.SingleNodeDistributedId,
            IsProductionTopology = true,
            Failures = ["Redis is missing.", "The lock is process-local."],
        };

        var result = ContactCenterTopologyHealthCheck.Evaluate(CreateContext(), verdict);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("Redis is missing.", result.Description, StringComparison.Ordinal);
        Assert.Contains("The lock is process-local.", result.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_ReportsHealthy_WhenTheDeclaredProductionTopologyIsSatisfied()
    {
        var verdict = new ContactCenterTopologyValidationResult
        {
            DeclaredProfileId = ContactCenterTopologyProfiles.SingleNodeDistributedId,
            IsProductionTopology = true,
        };

        var result = ContactCenterTopologyHealthCheck.Evaluate(CreateContext(), verdict);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains(ContactCenterTopologyProfiles.SingleNodeDistributedId, result.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_ReportsHealthy_WhenNoTopologyIsDeclaredOutsideProduction()
    {
        // A deployment that claims nothing is not failing anything. Reporting it unready would make every
        // development environment permanently drained.
        var verdict = new ContactCenterTopologyValidationResult
        {
            DeclaredProfileId = null,
            IsProductionTopology = false,
        };

        var result = ContactCenterTopologyHealthCheck.Evaluate(CreateContext(), verdict);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("does not claim production support", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_UsesTheConfiguredFailureStatus()
    {
        var verdict = new ContactCenterTopologyValidationResult
        {
            DeclaredProfileId = ContactCenterTopologyProfiles.SingleNodeDistributedId,
            IsProductionTopology = true,
            Failures = ["Redis is missing."],
        };

        var result = ContactCenterTopologyHealthCheck.Evaluate(CreateContext(HealthStatus.Degraded), verdict);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_ReadsTheRecordedVerdict()
    {
        // Arrange
        var state = new ContactCenterTopologyState();
        var check = new ContactCenterTopologyHealthCheck(state);

        var beforeValidation = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Act
        state.Record(new ContactCenterTopologyValidationResult
        {
            DeclaredProfileId = ContactCenterTopologyProfiles.SingleNodeDistributedId,
            IsProductionTopology = true,
        });

        var afterValidation = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, beforeValidation.Status);
        Assert.Equal(HealthStatus.Healthy, afterValidation.Status);
    }

    [Fact]
    public void State_IsNotAdmissible_UntilAVerdictIsRecorded()
    {
        var state = new ContactCenterTopologyState();

        Assert.False(state.IsAdmissible);
        Assert.Null(state.Result);
    }

    [Fact]
    public void State_IsNotAdmissible_WhenTheRecordedVerdictIsUnsatisfied()
    {
        var state = new ContactCenterTopologyState();

        state.Record(new ContactCenterTopologyValidationResult
        {
            DeclaredProfileId = ContactCenterTopologyProfiles.SingleNodeDistributedId,
            IsProductionTopology = true,
            Failures = ["Redis is missing."],
        });

        Assert.False(state.IsAdmissible);
    }

    [Fact]
    public void State_IsAdmissible_WhenTheRecordedVerdictIsSatisfied()
    {
        var state = new ContactCenterTopologyState();

        state.Record(new ContactCenterTopologyValidationResult
        {
            DeclaredProfileId = ContactCenterTopologyProfiles.SingleNodeDistributedId,
            IsProductionTopology = true,
        });

        Assert.True(state.IsAdmissible);
    }

    [Fact]
    public void State_Throws_WhenRecordingAMissingVerdict()
    {
        var state = new ContactCenterTopologyState();

        Assert.Throws<ArgumentNullException>(() => state.Record(null));
    }

    private static HealthCheckContext CreateContext(HealthStatus failureStatus = HealthStatus.Unhealthy)
        => new()
        {
            Registration = new HealthCheckRegistration(
                "contactcenter-topology",
                _ => throw new NotSupportedException("The registration factory is not used by these tests."),
                failureStatus,
                tags: null),
        };
}
