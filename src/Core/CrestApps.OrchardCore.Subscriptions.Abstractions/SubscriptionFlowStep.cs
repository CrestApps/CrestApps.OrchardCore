using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public sealed class SubscriptionFlowStep
{
    /// <summary>
    /// Each step must have a unique identifier.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// A title for the step.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// A description for the step.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// The position each step should appear.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Any payment information to collect before completing the subscription.
    /// </summary>
    public SubscriptionPayment Payment { get; set; }

    /// <summary>
    /// Allow adding custom data for the step.
    /// </summary>
    public Dictionary<string, object> Data { get; init; } = [];

    /// <summary>
    /// Whether or not to hide the step from rendering.
    /// </summary>
    [JsonIgnore]
    public bool Conceal { get; set; }
}
