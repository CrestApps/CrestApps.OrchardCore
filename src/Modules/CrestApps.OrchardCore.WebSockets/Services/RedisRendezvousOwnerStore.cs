using OrchardCore.Redis;
using StackExchange.Redis;

namespace CrestApps.OrchardCore.WebSockets.Services;

/// <summary>
/// Records rendezvous ownership in Redis, so every node in a deployment sees the same answer to "who is holding
/// this key".
/// </summary>
internal sealed class RedisRendezvousOwnerStore : IRendezvousOwnerStore
{
    private readonly IRedisService _redisService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisRendezvousOwnerStore"/> class.
    /// </summary>
    /// <param name="redisService">The tenant's Redis connection.</param>
    public RedisRendezvousOwnerStore(IRedisService redisService)
    {
        _redisService = redisService;
    }

    /// <inheritdoc/>
    public async Task<bool> TryTakeOwnershipAsync(string key, string nodeId, TimeSpan timeToLive, CancellationToken cancellationToken = default)
    {
        var database = await GetDatabaseAsync();

        // NotExists makes this the atomic claim: two nodes racing the same key cannot both win.
        return await database.StringSetAsync(GetKey(key), nodeId, timeToLive, When.NotExists);
    }

    /// <inheritdoc/>
    public async Task<string> GetOwnerAsync(string key, CancellationToken cancellationToken = default)
    {
        var database = await GetDatabaseAsync();
        var value = await database.StringGetAsync(GetKey(key));

        return value.HasValue ? value.ToString() : null;
    }

    /// <inheritdoc/>
    public async Task ReleaseAsync(string key, CancellationToken cancellationToken = default)
    {
        var database = await GetDatabaseAsync();

        await database.KeyDeleteAsync(GetKey(key));
    }

    private async Task<IDatabase> GetDatabaseAsync()
    {
        if (_redisService.Database is null)
        {
            await _redisService.ConnectAsync();
        }

        // Throwing rather than returning null: the registry treats an unreachable store as a reason to degrade to
        // node-local behaviour, and it can only do that if it is told.
        return _redisService.Database
            ?? throw new InvalidOperationException("The Redis connection is enabled but no database could be resolved.");
    }

    // The tenant's instance prefix keeps one tenant's rendezvous keys out of another's.
    private string GetKey(string key)
        => $"{_redisService.InstancePrefix}CrestApps:WebSockets:Rendezvous:{key}";
}
