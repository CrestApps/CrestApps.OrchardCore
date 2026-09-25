namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routing;

/// <summary>
/// The default <see cref="IMessagingRoutingStrategy"/> for a tenant without the routed-distribution feature. It
/// selects nobody, so every caller takes its shared-pool path: a tenant that has not opted into push assignment
/// gets the pooled behaviour it configured, rather than an assignment made by a policy it does not have.
/// </summary>
public sealed class NoMessagingRoutingStrategy : IMessagingRoutingStrategy
{
    /// <inheritdoc/>
    public Task<string> SelectAgentAsync(string queueId, string excludeAgentId = null, CancellationToken cancellationToken = default)
        => Task.FromResult<string>(null);
}
