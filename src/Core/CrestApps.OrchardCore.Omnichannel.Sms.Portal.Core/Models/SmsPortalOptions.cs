namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;

/// <summary>
/// Tunables for the SMS Portal itself, bound from the <c>CrestApps:Sms:Workspace</c> configuration section.
/// Every value that affects how long a caller waits or how much work one page does is configuration rather than
/// a constant, so an operator can tune a busy tenant without a code change.
/// </summary>
public sealed class SmsPortalOptions
{
    /// <summary>
    /// Gets or sets how long, in seconds, an inbound message waits for the per-thread lock before the delivery is
    /// failed back to the provider inbox for retry. Defaults to 10.
    /// </summary>
    public int ConversationLockTimeoutSeconds { get; set; } = 10;

    /// <summary>
    /// Gets or sets how long, in seconds, the per-thread lock is held before it expires on its own, so a node that
    /// dies mid-message cannot block the thread forever. Defaults to 30.
    /// </summary>
    public int ConversationLockExpirationSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets how many conversations one inbox page shows. Defaults to 50.
    /// </summary>
    public int InboxPageSize { get; set; } = 50;

    /// <summary>
    /// Gets or sets how many outbound messages one outbox pass examines. Defaults to 200.
    /// </summary>
    public int OutboxBatchSize { get; set; } = 200;

    /// <summary>
    /// Gets or sets how many messages one outbox pass may send from a single number, so a backlog on one
    /// endpoint cannot burst past the rate its carrier permits. Defaults to 20.
    /// </summary>
    public int MaxMessagesPerPassPerEndpoint { get; set; } = 20;

    /// <summary>
    /// Gets the per-thread lock wait as a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan ConversationLockTimeout => TimeSpan.FromSeconds(Math.Max(1, ConversationLockTimeoutSeconds));

    /// <summary>
    /// Gets the per-thread lock expiration as a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan ConversationLockExpiration => TimeSpan.FromSeconds(Math.Max(1, ConversationLockExpirationSeconds));
}
