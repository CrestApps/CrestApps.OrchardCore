using CrestApps.OrchardCore.ContactCenter.DistributedTests.Infrastructure;
using Microsoft.AspNetCore.SignalR.Client;

namespace CrestApps.OrchardCore.ContactCenter.DistributedTests;

public sealed class TwoNodeRedisBackplaneTests : IAsyncLifetime
{
    private const string AgentId = "shared-agent";

    private DistributedSignalRTestHost _tenantAOnNodeA;
    private DistributedSignalRTestHost _tenantAOnNodeB;
    private DistributedSignalRTestHost _tenantBOnNodeB;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var redisConfiguration = Environment.GetEnvironmentVariable("CONTACT_CENTER_REDIS_CONFIGURATION");

        if (string.IsNullOrWhiteSpace(redisConfiguration))
        {
            throw new InvalidOperationException(
                "CONTACT_CENTER_REDIS_CONFIGURATION must point to the Redis instance used by the distributed Contact Center tests.");
        }

        _tenantAOnNodeA = new("node-a", "TenantA", redisConfiguration);
        _tenantAOnNodeB = new("node-b", "TenantA", redisConfiguration);
        _tenantBOnNodeB = new("node-b", "TenantB", redisConfiguration);

        await _tenantAOnNodeA.StartAsync(cancellationToken);
        await _tenantAOnNodeB.StartAsync(cancellationToken);
        await _tenantBOnNodeB.StartAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_tenantBOnNodeB is not null)
        {
            await _tenantBOnNodeB.DisposeAsync();
        }

        if (_tenantAOnNodeB is not null)
        {
            await _tenantAOnNodeB.DisposeAsync();
        }

        if (_tenantAOnNodeA is not null)
        {
            await _tenantAOnNodeA.DisposeAsync();
        }
    }

    [Fact]
    public async Task ProviderEvent_CrossesNodesWithoutCrossingTenantShells()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var tenantAEvents = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tenantBEventCount = 0;
        await using var tenantAConnection = CreateConnection(_tenantAOnNodeB, eventId =>
        {
            tenantAEvents.TrySetResult(eventId);
        });
        await using var tenantBConnection = CreateConnection(_tenantBOnNodeB, _ =>
        {
            Interlocked.Increment(ref tenantBEventCount);
        });

        await tenantAConnection.StartAsync(cancellationToken);
        await tenantBConnection.StartAsync(cancellationToken);
        Assert.Equal("TenantA", await tenantAConnection.InvokeAsync<string>("Ready", cancellationToken));
        Assert.Equal("TenantB", await tenantBConnection.InvokeAsync<string>("Ready", cancellationToken));
        await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);

        // Act
        const string eventId = "provider-event-001";
        await _tenantAOnNodeA.GetProviderListener().PublishAsync(AgentId, eventId);
        var receivedEventId = await tenantAEvents.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);

        // Assert
        Assert.Equal(eventId, receivedEventId);
        Assert.Equal(0, Volatile.Read(ref tenantBEventCount));
    }

    private static HubConnection CreateConnection(
        DistributedSignalRTestHost host,
        Action<string> onProviderEvent)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl($"{host.BaseUrl}/distributed?userId={Uri.EscapeDataString(AgentId)}")
            .Build();

        connection.On("ProviderEvent", onProviderEvent);

        return connection;
    }
}
