using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.AI.Core.Webhooks;

/// <summary>
/// An outside endpoint that receives signed AI and contact center events.
/// </summary>
public sealed class AIWebhookSubscription : CatalogItem, INameAwareModel
{
    /// <summary>
    /// Gets or sets the subscription's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the HTTPS address events are posted to.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the events sent, from <see cref="AIWebhookEvents"/>.
    /// </summary>
    public List<string> Events { get; set; } = [];

    /// <summary>
    /// Gets or sets the signing secret, encrypted with the data protector.
    /// </summary>
    public string ProtectedSecret { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether events are sent.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets when the subscription was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// One event sent, or to be sent, to a subscription, with every attempt's outcome.
/// </summary>
public sealed class AIWebhookDelivery : CatalogItem
{
    /// <summary>
    /// Gets or sets the subscription.
    /// </summary>
    public string SubscriptionId { get; set; }

    /// <summary>
    /// Gets or sets the event.
    /// </summary>
    public string EventName { get; set; }

    /// <summary>
    /// Gets or sets the JSON body posted.
    /// </summary>
    public string Payload { get; set; }

    /// <summary>
    /// Gets or sets where the delivery stands.
    /// </summary>
    public AIWebhookDeliveryStatus Status { get; set; }

    /// <summary>
    /// Gets or sets how many times it was attempted.
    /// </summary>
    public int Attempts { get; set; }

    /// <summary>
    /// Gets or sets the HTTP status of the last attempt, if it got an answer.
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Gets or sets why the last attempt failed.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets when the event happened.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the next attempt is due.
    /// </summary>
    public DateTime NextAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was delivered or given up.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }
}

/// <summary>
/// Where a webhook delivery stands.
/// </summary>
public enum AIWebhookDeliveryStatus
{
    /// <summary>
    /// Waiting for its next attempt.
    /// </summary>
    Pending,

    /// <summary>
    /// The endpoint accepted it.
    /// </summary>
    Delivered,

    /// <summary>
    /// Every attempt failed, or the endpoint refused it for good.
    /// </summary>
    Failed,
}

/// <summary>
/// The events a webhook subscription can receive.
/// </summary>
public static class AIWebhookEvents
{
    /// <summary>
    /// An AI conversation started, on any channel.
    /// </summary>
    public const string ConversationStarted = "conversation.started";

    /// <summary>
    /// An AI conversation ended.
    /// </summary>
    public const string ConversationEnded = "conversation.ended";

    /// <summary>
    /// An AI conversation's post-session analysis finished.
    /// </summary>
    public const string ConversationAnalyzed = "conversation.analyzed";

    /// <summary>
    /// An AI call was handed to a person or transferred to a number.
    /// </summary>
    public const string ConversationTransferred = "conversation.transferred";

    /// <summary>
    /// An AI conversation was scored.
    /// </summary>
    public const string ConversationEvaluated = "conversation.evaluated";

    /// <summary>
    /// An AI alert rule crossed its threshold.
    /// </summary>
    public const string AlertTriggered = "alert.triggered";

    /// <summary>
    /// An AI alert rule is back within its threshold.
    /// </summary>
    public const string AlertResolved = "alert.resolved";

    /// <summary>
    /// A test event sent from the admin.
    /// </summary>
    public const string Test = "webhook.test";

    /// <summary>
    /// Every event a subscription can choose.
    /// </summary>
    public static readonly IReadOnlyList<string> All =
    [
        ConversationStarted,
        ConversationEnded,
        ConversationAnalyzed,
        ConversationTransferred,
        ConversationEvaluated,
        AlertTriggered,
        AlertResolved,
    ];
}
