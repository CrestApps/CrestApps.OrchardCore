using CrestApps.OrchardCore.ContactCenter.Core.HealthChecks;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Verifies that a production deployment whose base-voice audio path was never verified refuses traffic, while a
/// non-production host is only warned.
/// </summary>
/// <remarks>
/// The gate only matters if the unacknowledged state actually withholds readiness in production and does not
/// withhold it elsewhere. These tests pin both directions so the fail-closed behaviour cannot silently become a
/// warning, and the non-production tolerance cannot silently become a block.
/// </remarks>
public sealed class ContactCenterBaseVoiceVerificationHealthCheckTests
{
    [Fact]
    public void Evaluate_FailsClosed_WhenUnacknowledgedInProduction()
    {
        var result = ContactCenterBaseVoiceVerificationHealthCheck.Evaluate(
            CreateContext(),
            acknowledged: false,
            isProductionEnvironment: true,
            evidenceReference: null);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Contains("not been verified", result.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Readiness is withheld", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ReportsHealthy_WhenUnacknowledgedOutsideProduction()
    {
        // A development or test host must not be permanently drained by a verification step that only a real
        // deployment can perform.
        var result = ContactCenterBaseVoiceVerificationHealthCheck.Evaluate(
            CreateContext(),
            acknowledged: false,
            isProductionEnvironment: false,
            evidenceReference: null);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("tolerated outside a production host", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_ReportsHealthy_WhenAcknowledgedInProduction()
    {
        var result = ContactCenterBaseVoiceVerificationHealthCheck.Evaluate(
            CreateContext(),
            acknowledged: true,
            isProductionEnvironment: true,
            evidenceReference: null);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("verified and acknowledged", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_SurfacesTheEvidenceReference_WhenAcknowledged()
    {
        var result = ContactCenterBaseVoiceVerificationHealthCheck.Evaluate(
            CreateContext(),
            acknowledged: true,
            isProductionEnvironment: true,
            evidenceReference: "run-2026-07-14");

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("run-2026-07-14", result.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_UsesTheConfiguredFailureStatus()
    {
        var result = ContactCenterBaseVoiceVerificationHealthCheck.Evaluate(
            CreateContext(HealthStatus.Degraded),
            acknowledged: false,
            isProductionEnvironment: true,
            evidenceReference: null);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_FailsClosed_WhenUnacknowledgedInAProductionHost()
    {
        // Arrange
        var check = new ContactCenterBaseVoiceVerificationHealthCheck(
            Options.Create(new BaseVoiceVerificationOptions { AudioVerificationAcknowledged = false }),
            CreateEnvironment(Environments.Production));

        // Act
        var result = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_ReportsHealthy_WhenAcknowledgedInAProductionHost()
    {
        // Arrange
        var check = new ContactCenterBaseVoiceVerificationHealthCheck(
            Options.Create(new BaseVoiceVerificationOptions
            {
                AudioVerificationAcknowledged = true,
                AudioVerificationEvidenceReference = "reference-artifact",
            }),
            CreateEnvironment(Environments.Production));

        // Act
        var result = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("reference-artifact", result.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckHealthAsync_ReportsHealthy_WhenUnacknowledgedOutsideAProductionHost()
    {
        // Arrange
        var check = new ContactCenterBaseVoiceVerificationHealthCheck(
            Options.Create(new BaseVoiceVerificationOptions { AudioVerificationAcknowledged = false }),
            CreateEnvironment(Environments.Development));

        // Act
        var result = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    private static IHostEnvironment CreateEnvironment(string environmentName)
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(instance => instance.EnvironmentName).Returns(environmentName);

        return environment.Object;
    }

    private static HealthCheckContext CreateContext(HealthStatus failureStatus = HealthStatus.Unhealthy)
        => new()
        {
            Registration = new HealthCheckRegistration(
                "contactcenter-base-voice-verification",
                _ => throw new NotSupportedException("The registration factory is not used by these tests."),
                failureStatus,
                tags: null),
        };
}
