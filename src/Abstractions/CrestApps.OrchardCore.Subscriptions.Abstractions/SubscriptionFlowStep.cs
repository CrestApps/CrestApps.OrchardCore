using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Represents one step in a subscription checkout flow.
/// </summary>
public sealed class SubscriptionFlowStep
{
    /// <summary>
    /// Gets or sets the unique key that identifies the step within the flow.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the title shown for the step.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the description shown for the step.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the position used to order the step within the flow.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the step must store data in
    /// <see cref="ISubscriptionFlowSession.SavedSteps"/> before it is considered complete.
    /// </summary>
    public bool CollectData { get; set; }

    /// <summary>
    /// Gets or sets the billing items to collect before completing the subscription.
    /// </summary>
    public BillingItem[] BillingItems { get; set; }

    /// <summary>
    /// Gets the custom data associated with the step.
    /// </summary>
    public Dictionary<string, object> Data { get; init; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the step is hidden from rendering.
    /// </summary>
    [JsonIgnore]
    public bool Conceal { get; set; }
}
