using CrestApps.OrchardCore.ContactCenter.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Redis;
using StackExchange.Redis;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Verifies the Redis dependency probes report healthy with nothing probed when Redis is not enabled.
/// </summary>
/// <remarks>
/// A deployment that declares no Redis dependency has none to be unhealthy about, and the topology validator —
/// not these probes — decides whether Redis is required. Reporting a failure here would make a supported
/// development or single-node deployment alert as broken.
/// </remarks>
public sealed class ContactCenterRedisHealthCheckTests
{
    [Fact]
    public async Task RedisConnectivity_ReportsHealthy_WhenRedisIsNotEnabled()
    {
        // Arrange
        var check = new ContactCenterRedisConnectivityHealthCheck(redisService: null);

        // Act
        var result = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("not enabled", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Backplane_ReportsHealthy_WhenRedisIsNotEnabled()
    {
        // Arrange
        var check = new ContactCenterBackplaneHealthCheck(
            Options.Create(new RedisOptions()),
            CreateShellSettings(),
            redisService: null);

        // Act
        var result = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Contains("not enabled", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    private static ShellSettings CreateShellSettings()
        => new() { Name = "Default" };

    [Fact]
    public async Task Backplane_UnsubscribesTheProbeChannel_WhenTheSubscriptionTimesOut()
    {
        // Arrange
        var neverCompletes = new TaskCompletionSource<ChannelMessageQueue>(TaskCreationOptions.RunContinuationsAsynchronously);

        var subscriber = new Mock<ISubscriber>(MockBehavior.Strict);
        subscriber
            .Setup(s => s.SubscribeAsync(It.IsAny<RedisChannel>(), It.IsAny<Action<RedisChannel, RedisValue>>(), It.IsAny<CommandFlags>()))
            .Returns(neverCompletes.Task);
        subscriber
            .Setup(s => s.UnsubscribeAsync(It.IsAny<RedisChannel>(), It.IsAny<Action<RedisChannel, RedisValue>>(), It.IsAny<CommandFlags>()))
            .Returns(Task.CompletedTask);

        var connection = new Mock<IConnectionMultiplexer>(MockBehavior.Loose);
        connection.SetupGet(c => c.IsConnected).Returns(true);
        connection.Setup(c => c.GetSubscriber(It.IsAny<object>())).Returns(subscriber.Object);

        var redisService = new Mock<IRedisService>(MockBehavior.Loose);
        redisService.Setup(r => r.ConnectAsync()).Returns(Task.CompletedTask);
        redisService.SetupGet(r => r.Connection).Returns(connection.Object);

        var check = new ContactCenterBackplaneHealthCheck(
            Options.Create(new RedisOptions()),
            CreateShellSettings(),
            redisService.Object);

        // Act
        var result = await check.CheckHealthAsync(CreateContext(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        subscriber.Verify(
            s => s.UnsubscribeAsync(It.IsAny<RedisChannel>(), It.IsAny<Action<RedisChannel, RedisValue>>(), It.IsAny<CommandFlags>()),
            Times.Once);
    }

    private static HealthCheckContext CreateContext()
        => new()
        {
            Registration = new HealthCheckRegistration(
                "redis-probe",
                _ => throw new NotSupportedException("The registration factory is not used by these tests."),
                failureStatus: HealthStatus.Unhealthy,
                tags: null),
        };
}
