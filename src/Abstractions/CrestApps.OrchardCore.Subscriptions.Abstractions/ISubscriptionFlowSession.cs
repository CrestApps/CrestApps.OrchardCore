using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Subscriptions;

/// <summary>
/// The read/write surface of a subscription checkout session as seen by the subscription flow. It exposes
/// the identity of the session, its progress through the steps, and the ownership metadata.
/// </summary>
public interface ISubscriptionFlowSession : IEntity
{
    /// <summary>
    /// Gets or sets the unique identifier of the session.
    /// </summary>
    string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the key of the step the flow is currently on.
    /// </summary>
    string CurrentStep { get; set; }

    /// <summary>
    /// Gets or sets the current status of the session.
    /// </summary>
    SubscriptionSessionStatus Status { get; set; }

    /// <summary>
    /// Gets the ordered list of steps that make up the subscription flow.
    /// </summary>
    IList<SubscriptionFlowStep> Steps { get; }

    /// <summary>
    /// Gets the persisted state contributed by each completed step, keyed by step.
    /// </summary>
    JsonObject SavedSteps { get; }

    /// <summary>
    /// Gets or sets the identifier of the authenticated user that owns the session, when applicable.
    /// </summary>
    string OwnerId { get; set; }
}
