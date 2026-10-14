namespace CrestApps.OrchardCore.Subscriptions.Models;

/// <summary>
/// An entry in an <see cref="InstallmentPlan"/>'s history.
/// </summary>
public sealed class InstallmentPlanEvent
{
    /// <summary>
    /// Gets or sets when it happened.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets what happened.
    /// </summary>
    public InstallmentPlanEventType Type { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the payment number the entry is about, when it is about one.
    /// </summary>
    public int? PaymentNumber { get; set; }

    /// <summary>
    /// Gets or sets the name of the person who caused it, when a person did.
    /// </summary>
    public string ActorName { get; set; }
}
