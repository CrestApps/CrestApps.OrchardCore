using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Subscriptions;

/// <summary>
/// A durable, resumable subscription checkout session. It captures the subscription being purchased, the
/// progress through the flow steps, and the ownership metadata used to prevent another visitor from
/// resuming an anonymous session.
/// </summary>
public sealed class SubscriptionSession : Entity, ISubscriptionFlowSession
{
    /// <summary>
    /// Gets or sets the unique identifier of the session.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the content type of the subscription being purchased.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the subscription content item being purchased.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the version identifier of the subscription content item being purchased.
    /// </summary>
    public string ContentItemVersionId { get; set; }

    /// <summary>
    /// Gets or sets the current status of the session.
    /// </summary>
    public SubscriptionSessionStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC time at which the session was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time at which the session was last modified.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time at which the session was completed, when applicable.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the authenticated user that owns the session, when applicable.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets the persisted state contributed by each completed step, keyed by step.
    /// </summary>
    public JsonObject SavedSteps { get; init; } = [];

    /// <summary>
    /// Gets the ordered list of steps that make up the subscription flow.
    /// </summary>
    public IList<SubscriptionFlowStep> Steps { get; init; } = [];

    /// <summary>
    /// Gets or sets the key of the step the flow is currently on.
    /// </summary>
    public string CurrentStep { get; set; }

    /// <summary>
    /// Gets or sets the client IP address captured when an anonymous session was created.
    /// </summary>
    public string IPAddress { get; set; }

    /// <summary>
    /// Gets or sets the client user-agent captured when an anonymous session was created.
    /// </summary>
    public string AgentInfo { get; set; }
}
