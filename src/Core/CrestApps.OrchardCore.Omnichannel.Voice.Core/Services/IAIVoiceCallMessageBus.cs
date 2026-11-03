namespace CrestApps.OrchardCore.Omnichannel.Voice.Services;

/// <summary>
/// A publish and subscribe channel shared by every node of a tenant, which carries commands for live AI calls to the
/// node holding the call.
/// </summary>
public interface IAIVoiceCallMessageBus
{
    /// <summary>
    /// Publishes a message on a channel.
    /// </summary>
    /// <param name="channel">The channel name, without any tenant prefix.</param>
    /// <param name="message">The message.</param>
    /// <returns>A task that completes when the message was handed to the bus.</returns>
    Task PublishAsync(string channel, string message);

    /// <summary>
    /// Subscribes to a channel.
    /// </summary>
    /// <param name="channel">The channel name, without any tenant prefix.</param>
    /// <param name="handler">Called with every message published on the channel, by any node.</param>
    /// <returns>A task that completes when the subscription is in place.</returns>
    Task SubscribeAsync(string channel, Func<string, Task> handler);
}
