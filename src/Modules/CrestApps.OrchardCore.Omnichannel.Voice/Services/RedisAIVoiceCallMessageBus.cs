using OrchardCore.Environment.Shell;
using OrchardCore.Redis;
using StackExchange.Redis;

namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// Carries live AI call commands between a tenant's nodes over Redis publish and subscribe.
/// </summary>
internal sealed class RedisAIVoiceCallMessageBus : IAIVoiceCallMessageBus
{
    private readonly IRedisService _redisService;
    private readonly string _tenantName;

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisAIVoiceCallMessageBus"/> class.
    /// </summary>
    /// <param name="redisService">The tenant's Redis connection.</param>
    /// <param name="shellSettings">The tenant, whose name keeps its channels apart from other tenants'.</param>
    public RedisAIVoiceCallMessageBus(IRedisService redisService, ShellSettings shellSettings)
    {
        _redisService = redisService;
        _tenantName = shellSettings.Name;
    }

    /// <inheritdoc/>
    public async Task PublishAsync(string channel, string message)
    {
        var subscriber = await GetSubscriberAsync();

        await subscriber.PublishAsync(GetChannel(channel), message);
    }

    /// <inheritdoc/>
    public async Task SubscribeAsync(string channel, Func<string, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var subscriber = await GetSubscriberAsync();

        // Handled off the multiplexer's thread: a handler may run a live session's command, which must not hold up
        // every other message on the connection.
        await subscriber.SubscribeAsync(GetChannel(channel), (_, value) => Task.Run(() => handler(value.ToString())));
    }

    private async Task<ISubscriber> GetSubscriberAsync()
    {
        if (_redisService.Connection is null)
        {
            await _redisService.ConnectAsync();
        }

        return _redisService.Connection?.GetSubscriber()
            ?? throw new InvalidOperationException("The Redis connection is enabled but could not be opened.");
    }

    private RedisChannel GetChannel(string channel)
        => RedisChannel.Literal($"{_redisService.InstancePrefix}{_tenantName}:{channel}");
}
