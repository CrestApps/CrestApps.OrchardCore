using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// A single step in a checkout flow. Steps are contributed by feature handlers and ordered by
/// <see cref="Order"/>. Steps that carry <see cref="BillingItems"/> contribute to the checkout invoice.
/// </summary>
public sealed class CheckoutFlowStep
{
    /// <summary>
    /// A unique identifier for the step.
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
    /// The position the step appears in.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// When <see langword="true"/>, the step is not considered complete until data for it has been stored
    /// in <see cref="ICheckoutFlowSession.SavedSteps"/>.
    /// </summary>
    public bool CollectData { get; set; }

    /// <summary>
    /// The billing items this step contributes to the checkout invoice.
    /// </summary>
    public BillingItem[] BillingItems { get; set; }

    /// <summary>
    /// Additional step-specific data.
    /// </summary>
    public Dictionary<string, object> Data { get; init; } = [];

    /// <summary>
    /// When <see langword="true"/>, the step is hidden from rendering.
    /// </summary>
    [JsonIgnore]
    public bool Conceal { get; set; }
}
