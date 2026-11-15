namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// The person, queue and campaign the workspace's customer list is narrowed to, on top of its tab and channel.
/// </summary>
/// <param name="AgentId">The agent profile whose conversations are listed, or <see langword="null"/> for everybody's.</param>
/// <param name="QueueId">The queue whose conversations are listed, or <see langword="null"/> for every queue's.</param>
/// <param name="CampaignId">The campaign whose conversations are listed, or <see langword="null"/> for every campaign's.</param>
public sealed record MessagingInboxNarrowing(string AgentId, string QueueId, string CampaignId)
{
    /// <summary>
    /// Gets the narrowing that lists every conversation.
    /// </summary>
    public static MessagingInboxNarrowing None { get; } = new(null, null, null);
}
