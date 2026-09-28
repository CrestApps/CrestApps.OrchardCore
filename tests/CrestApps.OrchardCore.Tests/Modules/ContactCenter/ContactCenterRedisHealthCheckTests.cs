using CrestApps.OrchardCore.ContactCenter.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Redis;
using StackExchange.Redis;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Verifies the Redis backplane probe tears its probe subscription down even when the round-trip times out.
/// </summary>
/// <remarks>
/// The probe subscribes to an invocation-unique channel before publishing. When the round-trip abandons the
/// await on timeout, the underlying subscription is not cancelled, so the handler must be unsubscribed
/// explicitly or the connection accumulates a dead subscription on every failed probe.
/// </remarks>
public sealed class ContactCenterRedisHealthCheckTests
{
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
