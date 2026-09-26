using CrestApps.OrchardCore.WebSockets;
using CrestApps.OrchardCore.WebSockets.Services;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Environment.Shell.Removing;
using OrchardCore.Redis;
using StackExchange.Redis;

namespace CrestApps.OrchardCore.ContactCenter.DistributedTests;

/// <summary>
/// The rendezvous registry against a real Redis, with two registries standing in for two nodes.
/// <para>
/// This is the part the in-process tests cannot prove: that the ownership claim is genuinely atomic across
/// processes, and that a node which did not start a call learns from the shared store that another node did.
/// Everything else about the registry is decided locally; this is the bit that only Redis can answer.
/// </para>
/// </summary>
public sealed class TwoNodeWebSocketRendezvousTests : IAsyncLifetime
{
    private TestRedisService _redis;

    public async ValueTask InitializeAsync()
    {
        var redisConfiguration = Environment.GetEnvironmentVariable("CONTACT_CENTER_REDIS_CONFIGURATION");

        if (string.IsNullOrWhiteSpace(redisConfiguration))
        {
            throw new InvalidOperationException(
                "CONTACT_CENTER_REDIS_CONFIGURATION must point to the Redis instance used by the distributed Contact Center tests.");
        }

        _redis = new TestRedisService($"rendezvous-tests:{Guid.NewGuid():n}:");

        await _redis.ConnectAsync(redisConfiguration);
    }

    public async ValueTask DisposeAsync()
    {
        if (_redis is not null)
        {
            await _redis.DisposeAsync();
        }
    }

    [Fact]
    public async Task ANodeThatRegisters_HandsTheSocketOverToItself()
    {
        // Arrange
        var key = NewKey();
        var nodeA = CreateRegistry("node-a");
        var rendezvous = await nodeA.RegisterAsync(key, TestContext.Current.CancellationToken);

        // Act
        var claimed = await nodeA.TryClaimAsync(key, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(rendezvous, claimed);
    }

    [Fact]
    public async Task ACallbackThatLandsOnTheWrongNode_IsRefusedAndLeftForTheRightOne()
    {
        // Arrange
        // A live socket does not move between processes, so the wrong node cannot serve it. What it must not do
        // is tear the rendezvous down: the provider's retry may reach the node that can.
        var key = NewKey();
        var nodeA = CreateRegistry("node-a");
        var nodeB = CreateRegistry("node-b");
        var rendezvous = await nodeA.RegisterAsync(key, TestContext.Current.CancellationToken);

        // Act
        var wrongNode = await nodeB.TryClaimAsync(key, TestContext.Current.CancellationToken);
        var rightNode = await nodeA.TryClaimAsync(key, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(wrongNode);
        Assert.Same(rendezvous, rightNode);
    }

    [Fact]
    public async Task TwoNodesRacingTheSameKey_CannotBothWin()
    {
        // Arrange
        // Two nodes holding one correlation key means the provider's callback binds to whichever wins, and the
        // other call is left with no media at all. Only Redis can prove the claim is atomic.
        var key = NewKey();
        var nodeA = CreateRegistry("node-a");
        var nodeB = CreateRegistry("node-b");

        // Act
        var results = await Task.WhenAll(
            RegisterOutcomeAsync(nodeA, key),
            RegisterOutcomeAsync(nodeB, key));

        // Assert
        Assert.Equal(1, results.Count(succeeded => succeeded));
    }

    [Fact]
    public async Task AClaimedKey_IsReleasedSoTheSameCallCanComeBack()
    {
        // Arrange
        var key = NewKey();
        var nodeA = CreateRegistry("node-a");
        var nodeB = CreateRegistry("node-b");
        await nodeA.RegisterAsync(key, TestContext.Current.CancellationToken);
        await nodeA.TryClaimAsync(key, TestContext.Current.CancellationToken);

        // Act
        // The key is free again, so a retry that starts on another node is not refused as a duplicate.
        var exception = await Record.ExceptionAsync(() => nodeB.RegisterAsync(key, TestContext.Current.CancellationToken));

        // Assert
        Assert.Null(exception);
    }

    private static async Task<bool> RegisterOutcomeAsync(DistributedWebSocketConnectionRegistry registry, string key)
    {
        try
        {
            await registry.RegisterAsync(key, TestContext.Current.CancellationToken);

            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static string NewKey()
        => Guid.NewGuid().ToString("n");

    private DistributedWebSocketConnectionRegistry CreateRegistry(string nodeId)
        => new(
            new RedisRendezvousOwnerStore(_redis),
            nodeId,
            NullLogger<DistributedWebSocketConnectionRegistry>.Instance);

    /// <summary>
    /// The tenant's Redis connection, outside a tenant. The store under test reads nothing else off it.
    /// </summary>
    private sealed class TestRedisService : IRedisService, IAsyncDisposable
    {
        private ConnectionMultiplexer _multiplexer;

        public TestRedisService(string instancePrefix)
        {
            InstancePrefix = instancePrefix;
        }

        public IConnectionMultiplexer Connection => _multiplexer;

        public IDatabase Database { get; private set; }

        public string InstancePrefix { get; }

        public async Task ConnectAsync(string configuration)
        {
            _multiplexer = await ConnectionMultiplexer.ConnectAsync(configuration);
            Database = _multiplexer.GetDatabase();
        }

        public Task ConnectAsync() => Task.CompletedTask;

        // IRedisService is a tenant service, so it carries the shell lifecycle events too. The store under test
        // never uses them.
        public Task ActivatingAsync() => Task.CompletedTask;

        public Task ActivatedAsync() => Task.CompletedTask;

        public Task TerminatingAsync() => Task.CompletedTask;

        public Task TerminatedAsync() => Task.CompletedTask;

        public Task RemovingAsync(ShellRemovingContext context) => Task.CompletedTask;

        public async ValueTask DisposeAsync()
        {
            if (_multiplexer is not null)
            {
                await _multiplexer.DisposeAsync();
            }
        }
    }
}
