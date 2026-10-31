using System.Collections.Concurrent;
using CrestApps.OrchardCore.Omnichannel.Voice.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Voice;

public sealed class AIVoiceCallCommandRelayTests
{
    [Fact]
    public async Task RelayAsync_ReachesALiveSessionHeldByAnotherNode()
    {
        // Arrange
        var bus = new InMemoryBus();
        var (holder, holderRelay) = CreateNode(bus);
        var (sender, senderRelay) = CreateNode(bus);
        var received = new List<AIVoiceCallCommand>();

        using var registration = holder.Register("act1", command =>
        {
            received.Add(command);

            return Task.FromResult(true);
        });

        await senderRelay.StartAsync((_, _) => Task.FromResult<bool?>(null));

        // Act
        var localOnly = await sender.SendAsync("act1", new AIVoiceCallCommand(AIVoiceCallCommandKind.Instruct, "offer 10%"));
        var relayed = await sender.RelayAsync("act1", new AIVoiceCallCommand(AIVoiceCallCommandKind.Instruct, "offer 10%"));

        // Assert
        Assert.False(localOnly);
        Assert.True(relayed);
        Assert.Equal("offer 10%", Assert.Single(received).Text);

        holderRelay.Dispose();
        senderRelay.Dispose();
    }

    [Fact]
    public async Task RelayAsync_ReportsWhatTheHoldingNodeAnswered()
    {
        // Arrange
        var bus = new InMemoryBus();
        var (holder, holderRelay) = CreateNode(bus);
        var (sender, senderRelay) = CreateNode(bus);

        using var registration = holder.Register("act1", _ => Task.FromResult(false));

        // Act
        var relayed = await sender.RelayAsync("act1", new AIVoiceCallCommand(AIVoiceCallCommandKind.TakeOver));

        // Assert
        Assert.False(relayed);

        holderRelay.Dispose();
        senderRelay.Dispose();
    }

    [Fact]
    public async Task RelayAsync_GivesUpWhenNoNodeHoldsTheCall()
    {
        // Arrange
        var bus = new InMemoryBus();
        var (holder, holderRelay) = CreateNode(bus);
        var (sender, senderRelay) = CreateNode(bus);
        senderRelay.ReplyTimeout = TimeSpan.FromMilliseconds(100);

        using var registration = holder.Register("act1", _ => Task.FromResult(true));

        // Act
        var relayed = await sender.RelayAsync("act-nobody", new AIVoiceCallCommand(AIVoiceCallCommandKind.Instruct, "hello"));

        // Assert
        Assert.False(relayed);

        holderRelay.Dispose();
        senderRelay.Dispose();
    }

    [Fact]
    public async Task RelayAsync_PrefersTheSessionOnThisNode()
    {
        // Arrange
        var bus = new InMemoryBus();
        var (node, relay) = CreateNode(bus);

        using var registration = node.Register("act1", _ => Task.FromResult(true));

        // Act
        var relayed = await node.RelayAsync("act1", new AIVoiceCallCommand(AIVoiceCallCommandKind.KeysPressed, "1"));

        // Assert
        Assert.True(relayed);
        Assert.Equal(0, bus.Published);

        relay.Dispose();
    }

    [Fact]
    public async Task RelayAsync_FallsBackWhenTheBusFails()
    {
        // Arrange
        var bus = new InMemoryBus { Fail = true };
        var (node, relay) = CreateNode(bus);

        // Act
        var relayed = await node.RelayAsync("act1", new AIVoiceCallCommand(AIVoiceCallCommandKind.Instruct, "hello"));

        // Assert
        Assert.False(relayed);

        relay.Dispose();
    }

    private static (AIVoiceCallCommands Commands, AIVoiceCallCommandRelay Relay) CreateNode(InMemoryBus bus)
    {
        var relay = new AIVoiceCallCommandRelay(bus, NullLogger<AIVoiceCallCommandRelay>.Instance);

        return (new AIVoiceCallCommands([relay]), relay);
    }

    private sealed class InMemoryBus : IAIVoiceCallMessageBus
    {
        private readonly ConcurrentDictionary<string, List<Func<string, Task>>> _subscribers = new(StringComparer.Ordinal);
        private int _published;

        public bool Fail { get; set; }

        public int Published => _published;

        public Task PublishAsync(string channel, string message)
        {
            Interlocked.Increment(ref _published);

            if (_subscribers.TryGetValue(channel, out var handlers))
            {
                foreach (var handler in handlers.ToArray())
                {
                    _ = Task.Run(() => handler(message));
                }
            }

            return Task.CompletedTask;
        }

        public Task SubscribeAsync(string channel, Func<string, Task> handler)
        {
            if (Fail)
            {
                throw new InvalidOperationException("The bus is down.");
            }

            _subscribers.GetOrAdd(channel, _ => []).Add(handler);

            return Task.CompletedTask;
        }
    }
}
